using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LocalAIStudio.Services
{
    public class ConnectionPermissionService
    {
        #region Singleton
        private static readonly Lazy<ConnectionPermissionService> _instance = 
            new Lazy<ConnectionPermissionService>(() => new ConnectionPermissionService());
        public static ConnectionPermissionService Instance => _instance.Value;
        #endregion

        private readonly ConcurrentDictionary<string, ConnectionRequest> _pendingConnections = new ConcurrentDictionary<string, ConnectionRequest>();
        private readonly ConcurrentDictionary<string, ConnectionPermission> _permissions = new ConcurrentDictionary<string, ConnectionPermission>();
        private System.Threading.Timer _cleanupTimer;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _permissionDuration = TimeSpan.FromHours(24);

        public event EventHandler<ConnectionRequest> ConnectionRequested;
        public event EventHandler<ConnectionRequest> ConnectionApproved;
        public event EventHandler<ConnectionRequest> ConnectionRejected;
        public event EventHandler<ConnectionRequest> ConnectionTimeout;

        public bool AcceptConnections { get; set; } = true;
        public bool RequireApproval { get; set; } = true;

        public ConnectionPermissionService()
        {
            StartCleanupTimer();
        }

        private void StartCleanupTimer()
        {
            _cleanupTimer = new System.Threading.Timer(CleanupExpiredRequests, null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private void CleanupExpiredRequests(object state)
        {
            var now = DateTime.Now;
            var expiredKeys = new List<string>();

            foreach (var kvp in _pendingConnections)
            {
                if (now - kvp.Value.RequestTime > _requestTimeout)
                {
                    expiredKeys.Add(kvp.Key);
                    ConnectionTimeout.Invoke(this, kvp.Value);
                }
            }

            foreach (var key in expiredKeys)
            {
                _pendingConnections.TryRemove(key, out _);
            }

            expiredKeys.Clear();
            foreach (var kvp in _permissions)
            {
                if (now - kvp.Value.GrantedTime > _permissionDuration)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _permissions.TryRemove(key, out _);
            }
        }

        public ConnectionRequest CreateConnectionRequest(string clientId, string clientIp, ConnectionType type)
        {
            if (!AcceptConnections)
            {
                return new ConnectionRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId = clientId,
                    ClientIp = clientIp,
                    Type = type,
                    Status = ConnectionStatus.Rejected,
                    StatusMessage = "连接已被服务器拒绝"
                };
            }

            var request = new ConnectionRequest
            {
                Id = Guid.NewGuid().ToString(),
                ClientId = clientId,
                ClientIp = clientIp,
                Type = type,
                Status = ConnectionStatus.Pending,
                RequestTime = DateTime.Now
            };

            _pendingConnections[request.Id] = request;
            ConnectionRequested.Invoke(this, request);

            if (!RequireApproval)
            {
                ApproveConnection(request.Id);
            }

            return request;
        }

        public bool ApproveConnection(string requestId)
        {
            if (!_pendingConnections.TryGetValue(requestId, out var request))
            {
                return false;
            }

            request.Status = ConnectionStatus.Approved;
            request.ApprovedTime = DateTime.Now;

            var permission = new ConnectionPermission
            {
                ClientId = request.ClientId,
                ClientIp = request.ClientIp,
                ConnectionType = request.Type,
                GrantedTime = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(_permissionDuration)
            };

            _permissions[$"{request.ClientId}:{request.Type}"] = permission;
            _pendingConnections.TryRemove(requestId, out _);

            ConnectionApproved.Invoke(this, request);
            return true;
        }

        public bool RejectConnection(string requestId, string reason = "")
        {
            if (!_pendingConnections.TryGetValue(requestId, out var request))
            {
                return false;
            }

            request.Status = ConnectionStatus.Rejected;
            request.StatusMessage = reason != "" ? reason : "连接已被用户拒绝";
            request.RejectedTime = DateTime.Now;

            _pendingConnections.TryRemove(requestId, out _);
            ConnectionRejected.Invoke(this, request);
            return true;
        }

        public bool ValidatePermission(string clientId, ConnectionType type, string clientIp = "")
        {
            var key = $"{clientId}:{type}";
            if (!_permissions.TryGetValue(key, out var permission))
            {
                return false;
            }

            if (DateTime.Now > permission.ExpiresAt)
            {
                _permissions.TryRemove(key, out _);
                return false;
            }

            if (!string.IsNullOrEmpty(clientIp) && permission.ClientIp != clientIp)
            {
                return false;
            }

            return true;
        }

        public void RevokePermission(string clientId, ConnectionType type)
        {
            var key = $"{clientId}:{type}";
            if (_permissions.TryRemove(key, out _))
            {
                System.Diagnostics.Debug.WriteLine($"已撤销权限: {clientId} - {type}");
            }
        }

        public void RevokeAllPermissions(string clientId)
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in _permissions)
            {
                if (kvp.Key.StartsWith($"{clientId}:"))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _permissions.TryRemove(key, out _);
            }
        }

        public void RevokeAllPermissions()
        {
            _permissions.Clear();
        }

        public ConnectionRequest[] GetPendingRequests()
        {
            return _pendingConnections.Values.ToArray();
        }

        public ConnectionPermission[] GetActivePermissions()
        {
            return _permissions.Values.ToArray();
        }

        public void SetAcceptConnections(bool accept)
        {
            AcceptConnections = accept;

            if (!accept)
            {
                foreach (var request in _pendingConnections.Values)
                {
                    RejectConnection(request.Id, "服务器已关闭连接");
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer.Dispose();
        }
    }

    #region Models

    public enum ConnectionType
    {
        RemoteDesktop,
        Camera,
        Microphone,
        FileTransfer,
        All
    }

    public enum ConnectionStatus
    {
        Pending,
        Approved,
        Rejected,
        Timeout,
        Disconnected
    }

    public class ConnectionRequest
    {
        public string Id { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientIp { get; set; } = "";
        public ConnectionType Type { get; set; }
        public ConnectionStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime ApprovedTime { get; set; }
        public DateTime RejectedTime { get; set; }
    }

    public class ConnectionPermission
    {
        public string ClientId { get; set; } = "";
        public string ClientIp { get; set; } = "";
        public ConnectionType ConnectionType { get; set; }
        public DateTime GrantedTime { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    #endregion
}
