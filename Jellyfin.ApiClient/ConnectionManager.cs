using Jellyfin.ApiClient.Model;
using Jellyfin.ApiClient.Net;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.ApiClient
{
    public class ConnectionManager : IConnectionManager
    {
        public event EventHandler<GenericEventArgs<UserDto>> LocalUserSignIn;
        public event EventHandler<GenericEventArgs<IApiClient>> LocalUserSignOut;
        public event EventHandler<EventArgs> RemoteLoggedOut;

        public event EventHandler<GenericEventArgs<ConnectionResult>> Connected;

        private readonly ICredentialProvider _credentialProvider;
        private readonly INetworkConnection _networkConnectivity;
        private readonly ILogger _logger;
        private readonly IServerLocator _serverDiscovery;
        private readonly IAsyncHttpClient _httpClient;
        private readonly Func<IClientWebSocket> _webSocketFactory;

        public Dictionary<string, IApiClient> ApiClients { get; private set; }

        public string ApplicationName { get; private set; }
        public string ApplicationVersion { get; private set; }
        public IDevice Device { get; private set; }
        public ClientCapabilities ClientCapabilities { get; private set; }

        public IApiClient CurrentApiClient { get; private set; }

        public ConnectionManager(ILogger logger,
            ICredentialProvider credentialProvider,
            INetworkConnection networkConnectivity,
            IServerLocator serverDiscovery,
            string applicationName,
            string applicationVersion,
            IDevice device,
            ClientCapabilities clientCapabilities,
            Func<IClientWebSocket> webSocketFactory = null)
        {
            _credentialProvider = credentialProvider;
            _networkConnectivity = networkConnectivity;
            _logger = logger;
            _serverDiscovery = serverDiscovery;
            _httpClient = AsyncHttpClientFactory.Create(logger);
            ClientCapabilities = clientCapabilities;
            _webSocketFactory = webSocketFactory;

            Device = device;
            ApplicationVersion = applicationVersion;
            ApplicationName = applicationName;
            ApiClients = new Dictionary<string, IApiClient>(StringComparer.OrdinalIgnoreCase);
            SaveLocalCredentials = true;

            var jsonSerializer = new NewtonsoftJsonSerializer();
        }

        public IJsonSerializer JsonSerializer = new NewtonsoftJsonSerializer();

        public bool SaveLocalCredentials { get; set; }

        private IApiClient GetOrAddApiClient(ServerInfo server)
        {
            IApiClient apiClient;

            if (!ApiClients.TryGetValue(server.Id, out apiClient))
            {
                var address = server.Address;

                apiClient = new ApiClient(_logger, address, ApplicationName, Device, ApplicationVersion)
                {
                    JsonSerializer = JsonSerializer,
                };

                apiClient.OnAuthenticated += ApiClientOnAuthenticated;

                ApiClients[server.Id] = apiClient;
            }

            if (string.IsNullOrEmpty(server.AccessToken))
            {
                apiClient.ClearAuthenticationInfo();
            }
            else
            {
                apiClient.SetAuthenticationInfo(server.AccessToken, server.UserId);
            }

            return apiClient;
        }

        private async void ApiClientOnAuthenticated(object apiClient, GenericEventArgs<AuthenticationResult> result)
        {
            await OnAuthenticated((IApiClient)apiClient, result.Argument, new ConnectionOptions(), SaveLocalCredentials);
        }

        private async void AfterConnected(IApiClient apiClient, ConnectionOptions options)
        {
            if (options.ReportCapabilities)
            {
                try
                {
                    await apiClient.ReportCapabilities(ClientCapabilities).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error reporting capabilities", ex);
                }
            }

            if (options.EnableWebSocket)
            {
                if (_webSocketFactory != null)
                {
                    ((ApiClient)apiClient).OpenWebSocket(_webSocketFactory);
                }
            }
        }

        public async Task<List<ServerInfo>> GetAvailableServers(CancellationToken cancellationToken = default(CancellationToken))
        {
            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            _logger.LogDebug("{0} servers in saved credentials", credentials.Servers.Count);

            if (_networkConnectivity.GetNetworkStatus().GetIsAnyLocalNetworkAvailable())
            {
                foreach (var server in await FindServers(cancellationToken).ConfigureAwait(false))
                {
                    credentials.AddOrUpdateServer(server);
                }
            }

            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

            return credentials.Servers.OrderByDescending(i => i.DateLastAccessed).ToList();
        }

        private async Task<List<ServerInfo>> FindServers(CancellationToken cancellationToken)
        {
            List<ServerDiscoveryInfo> servers;

            try
            {
                servers = await _serverDiscovery.FindServers(1500, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("No servers found via local discovery.");

                servers = new List<ServerDiscoveryInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error discovering servers.", ex);

                servers = new List<ServerDiscoveryInfo>();
            }

            return servers.Select(i => new ServerInfo
            {
                Id = i.Id,
                Address = ConvertEndpointAddressToManualAddress(i) ?? i.Address,
                Name = i.Name
            })
            .ToList();
        }

        private string ConvertEndpointAddressToManualAddress(ServerDiscoveryInfo info)
        {
            if (!string.IsNullOrWhiteSpace(info.Address) && !string.IsNullOrWhiteSpace(info.EndpointAddress))
            {
                var address = info.EndpointAddress.Split(':').First();

                // Determine the port, if any
                var parts = info.Address.Split(':');
                if (parts.Length > 1)
                {
                    var portString = parts.Last();
                    int port;
                    if (int.TryParse(portString, NumberStyles.Any, CultureInfo.InvariantCulture, out port))
                    {
                        address += ":" + portString;
                    }
                }

                return NormalizeAddress(address);
            }

            return null;
        }

        public async Task<ConnectionResult> Connect(CancellationToken cancellationToken = default(CancellationToken))
        {
            var servers = await GetAvailableServers(cancellationToken).ConfigureAwait(false);

            return await Connect(servers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loops through a list of servers and returns the first that is available for connection
        /// </summary>
        private async Task<ConnectionResult> Connect(List<ServerInfo> servers, CancellationToken cancellationToken)
        {
            servers = servers
               .OrderByDescending(i => i.DateLastAccessed)
               .ToList();

            if (servers.Count == 1)
            {
                _logger.LogDebug("1 server in the list.");

                var result = await Connect(servers[0], cancellationToken).ConfigureAwait(false);

                if (result.State == ConnectionState.Unavailable)
                {
                    result.State = ConnectionState.ServerSelection;
                }

                return result;
            }

            var firstServer = servers.FirstOrDefault();
            // See if we have any saved credentials and can auto sign in
            if (firstServer != null && !string.IsNullOrEmpty(firstServer.AccessToken))
            {
                var result = await Connect(firstServer, cancellationToken).ConfigureAwait(false);

                if (result.State == ConnectionState.SignedIn)
                {
                    return result;
                }
            }

            var finalResult = new ConnectionResult
            {
                Servers = servers
            };

            finalResult.State = ConnectionState.ServerSelection;

            return finalResult;
        }

        /// <summary>
        /// Attempts to connect to a server
        /// </summary>
        public Task<ConnectionResult> Connect(ServerInfo server, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Connect(server, new ConnectionOptions(), cancellationToken);
        }

        public async Task<ConnectionResult> Connect(ServerInfo server, ConnectionOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {

            string address = server.Address;
            if (string.IsNullOrEmpty(address))
            {
                // TODO: on failed connection
                return new ConnectionResult { State = ConnectionState.Unavailable };
            }

            int timeout = 100;

            await TryConnect(address, timeout, cancellationToken);

            // TODO: this isn't right
            return new ConnectionResult { State = ConnectionState.Unavailable };
        }

        private async Task OnSuccessfulConnection(ServerInfo server,
            ConnectionOptions options,
            PublicSystemInfo systemInfo,
            ConnectionResult result,
            CancellationToken cancellationToken)
        {
            server.ImportInfo(systemInfo);

            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(server.AccessToken))
            {
                await ValidateAuthentication(server, options, cancellationToken).ConfigureAwait(false);
            }

            credentials.AddOrUpdateServer(server);

            if (options.UpdateDateLastAccessed)
            {
                server.DateLastAccessed = DateTime.UtcNow;
            }

            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

            result.ApiClient = GetOrAddApiClient(server);
            result.State = string.IsNullOrEmpty(server.AccessToken) ?
                ConnectionState.ServerSignIn :
                ConnectionState.SignedIn;

            ((ApiClient)result.ApiClient).EnableAutomaticNetworking(server, _networkConnectivity);

            if (result.State == ConnectionState.SignedIn)
            {
                AfterConnected(result.ApiClient, options);
            }

            CurrentApiClient = result.ApiClient;

            result.Servers.Add(server);

            Connected?.Invoke(this, new GenericEventArgs<ConnectionResult>(result));
        }

        public Task<ConnectionResult> Connect(IApiClient apiClient, CancellationToken cancellationToken = default(CancellationToken))
        {
            var client = (ApiClient)apiClient;
            return Connect(client.ServerInfo, cancellationToken);
        }

        private async Task ValidateAuthentication(ServerInfo server, ConnectionOptions options, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Validating saved authentication");

            var url = server.Address;

            var headers = new HttpHeaders();
            headers.SetAccessToken(server.AccessToken);

            var request = new HttpRequest
            {
                CancellationToken = cancellationToken,
                Method = "GET",
                RequestHeaders = headers,
                Url = url + "/emby/system/info?format=json"
            };

            try
            {
                using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    var systemInfo = JsonSerializer.DeserializeFromStream<SystemInfo>(stream);

                    server.ImportInfo(systemInfo);
                }

                if (server.UserId != Guid.Empty)
                {
                    request.Url = url + "/mediabrowser/users/" + server.UserId + "?format=json";

                    using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
                    {
                        var localUser = JsonSerializer.DeserializeFromStream<UserDto>(stream);

                        OnLocalUserSignIn(options, localUser);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Already logged at a lower level

                server.UserId = Guid.Empty;
                server.AccessToken = null;
            }
        }

        private async Task<PublicSystemInfo> TryConnect(string url, int timeout, CancellationToken cancellationToken)
        {
            url += "/emby/system/info/public?format=json";

            try
            {
                using (var stream = await _httpClient.SendAsync(new HttpRequest
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    Timeout = timeout,
                    Method = "GET"

                }).ConfigureAwait(false))
                {
                    return JsonSerializer.DeserializeFromStream<PublicSystemInfo>(stream);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Already logged at a lower level

                return null;
            }
        }

        /// <summary>
        /// Wakes all servers.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task WakeAllServers(CancellationToken cancellationToken)
        {
            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            foreach (var server in credentials.Servers.ToList())
            {
                await WakeServer(server, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wakes a server
        /// </summary>
        private async Task WakeServer(ServerInfo server, CancellationToken cancellationToken)
        {
            foreach (var info in server.WakeOnLanInfos)
            {
                await WakeServer(info, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wakes a device based on mac address
        /// </summary>
        private async Task WakeServer(WakeOnLanInfo info, CancellationToken cancellationToken)
        {
            try
            {
                await _networkConnectivity.SendWakeOnLan(info.MacAddress, info.Port, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending wake on lan command", ex);
            }
        }

        public void Dispose()
        {
            foreach (var client in ApiClients.Values.ToList())
            {
                client.Dispose();
            }
        }

        public IApiClient GetApiClient(IHasServerId item)
        {
            return GetApiClient(item.ServerId);
        }

        public IApiClient GetApiClient(string serverId)
        {
            return ApiClients.Values.OfType<ApiClient>().FirstOrDefault(i => string.Equals(i.ServerInfo.Id, serverId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<ConnectionResult> Connect(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            address = NormalizeAddress(address);

            var publicInfo = await TryConnect(address, 15000, cancellationToken).ConfigureAwait(false);

            if (publicInfo == null)
            {
                return new ConnectionResult
                {
                    State = ConnectionState.Unavailable
                };
            }

            var server = new ServerInfo
            {
                Address = address
            };

            server.ImportInfo(publicInfo);

            return await Connect(server, cancellationToken).ConfigureAwait(false);
        }

        private string NormalizeAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException("address");
            }

            if (!address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                address = "http://" + address;
            }

            return address;
        }

        private async Task OnAuthenticated(IApiClient apiClient,
            AuthenticationResult result,
            ConnectionOptions options,
            bool saveCredentials)
        {
            var server = ((ApiClient)apiClient).ServerInfo;

            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            if (options.UpdateDateLastAccessed)
            {
                server.DateLastAccessed = DateTime.UtcNow;
            }

            if (saveCredentials)
            {
                server.UserId = result.User.Id;
                server.AccessToken = result.AccessToken;
            }
            else
            {
                server.UserId = Guid.Empty;
                server.AccessToken = null;
            }

            credentials.AddOrUpdateServer(server);
            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

            AfterConnected(apiClient, options);

            OnLocalUserSignIn(options, result.User);
        }

        private void OnLocalUserSignIn(ConnectionOptions options, UserDto user)
        {
            // TODO: Create a separate property for this
            if (options.UpdateDateLastAccessed)
            {
                if (LocalUserSignIn != null)
                {
                    LocalUserSignIn(this, new GenericEventArgs<UserDto>(user));
                }
            }
        }

        private void OnLocalUserSignout(IApiClient apiClient)
        {
            if (LocalUserSignOut != null)
            {
                LocalUserSignOut(this, new GenericEventArgs<IApiClient>(apiClient));
            }
        }

        public async Task Logout()
        {
            foreach (var client in ApiClients.Values.ToList())
            {
                if (!string.IsNullOrEmpty(client.AccessToken))
                {
                    await client.Logout().ConfigureAwait(false);
                    OnLocalUserSignout(client);
                }
            }

            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            var servers = credentials.Servers.ToList();

            foreach (var server in servers)
            {
                server.AccessToken = null;
                server.UserId = Guid.Empty;
            }

            credentials.Servers = servers;

            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);
        }

        public async Task<ServerInfo> GetServerInfo(string id)
        {
            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            return credentials.Servers.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
