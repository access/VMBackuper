using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VMBackuper.Models;
using VMBackuper.Models.DTOs;

namespace VMBackuper.Services
{
    // --------- RELATIONS for SshService --------------
    public enum SshConnectionType
    {
        NONE,
        KEYBOARD_INTERACTIVE,
        DEFAULTCONNECTION
    }

    public class SshConnection
    {
        public bool Success = false;
        public SshConnectionType ConnectionType = SshConnectionType.NONE;
    }

    public class SshKeygenCommand
    {
        public bool isExists = false;
        public string SshKeygenCmd = string.Empty;
        public bool isESXiHost = false;
        public override string ToString() => $"isExists: {isExists} SshKeygenCmd: {SshKeygenCmd} isVmWare: {isESXiHost}";
    }

    public class HyperShell
    {
        public string Result { get; set; } = string.Empty;
        public bool Success { get; set; } = false;
        public int ExitStatus { get; set; } = -1;
    }
    // ---------------------------------------------

    public class HyperSshService
    {
        private static SshClient _sshClient;
        private HyperVisorItem _hvServer;
        private ConnectionInfo _keyboardConnectionInfo;
        private ConnectionInfo _defaultConnectionInfo;
        private int _connectionTimeoutSec = 5;
        private string _checkResult = "ConnectionOkWithTheRemoteMachine123456";
        private static string _checkResultForInstalledPackage = "PackageHasBeenDetectedOnTheRemoteMachine123456";
        private SshConnectionType _sshConnectionType = SshConnectionType.NONE;
        private KeyboardInteractiveAuthenticationMethod _keyboardAuth;
        private PasswordAuthenticationMethod _passwordAuth;
        private string _privateKeyContent = string.Empty;
        private string _pubKeyContent = string.Empty;
        private static string _remoteKeyFileName = "HyperVisor_ID_RSA_KEY";
        private string _remoteKeyFileNamePub = $"{_remoteKeyFileName}.pub";
        // publics
        public static string PrivateKeysDirectory = string.Empty;
        public static string ScriptsDirectory = string.Empty;
        public static string AssetsDirectory = string.Empty;
        public static string ESXiGhettoConfigsDirectory = "/opt/ghettovcb/";
        public string PrivateKeyContent => _privateKeyContent;
        public string PublicKeyContent => _pubKeyContent;

        public HyperSshService(HyperVisorItem hyperVisorItem, string Password)
        {
            _hvServer = hyperVisorItem;
            _keyboardAuth = new KeyboardInteractiveAuthenticationMethod(_hvServer.UserName);
            _passwordAuth = new PasswordAuthenticationMethod(_hvServer.UserName, Password);
            _keyboardAuth.AuthenticationPrompt += new EventHandler<AuthenticationPromptEventArgs>((sender, e) =>
            {
                foreach (AuthenticationPrompt prompt in e.Prompts)
                    if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                        prompt.Response = Password;
            });
            _keyboardConnectionInfo = new ConnectionInfo(_hvServer.HostName, _hvServer.Port, _hvServer.UserName, _passwordAuth, _keyboardAuth);
            _keyboardConnectionInfo.Timeout = TimeSpan.FromSeconds(_connectionTimeoutSec);
            var methods = new List<AuthenticationMethod>();
            methods.Add(new PasswordAuthenticationMethod(_hvServer.UserName, Password));
            _defaultConnectionInfo = new ConnectionInfo(_hvServer.HostName, _hvServer.Port, _hvServer.UserName, methods.ToArray());
            _defaultConnectionInfo.Timeout = TimeSpan.FromSeconds(_connectionTimeoutSec);
        }

        public static void SaveGhettoConfig(HyperVisorItem hv, string config)
        {
            // fix for unix systems, no need the (windows) \r\n, just \n
            string fixedCfg = Regex.Replace(config, @"\r+", string.Empty);
            var connectionInfo = GetSshClientInstanceBySavedCredentials(hv).ConnectionInfo;
            var sftp = GetSFtpClientInstanceByConnectionInfo(connectionInfo);
            if (!sftp.IsConnected)
                sftp.Connect();
            if (sftp.IsConnected)
            {
                string remoteCfgPath = ESXiGhettoConfigsDirectory + hv.GhettoBackupConfigFileName;
                sftp.Create(remoteCfgPath);
                sftp.WriteAllText(remoteCfgPath, fixedCfg);
            }
            sftp.Disconnect();
        }

        public static List<Folder> GetRecoveryList(HyperVisorItem hv)
        {
            //Folder backups = new Folder() { Name = "backups" };
            List<Folder> backups = new();
            string config = GetGhettoConfig(hv);
            string backupPath = string.Empty;
            var match = Regex.Match(config, @"^(?<OPTNAME>.?VM_BACKUP_VOLUME.?)\=(?<PATH>.+?)$", RegexOptions.Multiline);
            if (match.Success)
                backupPath = match.Groups["PATH"].Value.Trim();

            var connectionInfo = GetSshClientInstanceBySavedCredentials(hv).ConnectionInfo;
            var sftp = GetSFtpClientInstanceByConnectionInfo(connectionInfo);
            if (!sftp.IsConnected)
                sftp.Connect();
            if (sftp.IsConnected)
            {
                if (sftp.Exists(backupPath))
                {
                    var rootList = sftp.ListDirectory(backupPath);
                    // get recoveries items
                    foreach (var item in rootList)
                        if (item.Name != "." && item.Name != "..")
                            backups.Add(new Folder()
                            {
                                Name = item.Name,
                                FullName = item.FullName,
                                LastWriteTimeUtc = item.LastWriteTimeUtc
                            });
                    // get images list recovery dir
                    foreach (var item in backups)
                        if (sftp.Exists(item.FullName))
                            foreach (var dir in sftp.ListDirectory(item.FullName))
                                if (dir.Name != "." && dir.Name != "..")
                                    item.Directories.Add(new Folder()
                                    {
                                        Name = dir.Name,
                                        FullName = dir.FullName,
                                        LastWriteTimeUtc = dir.LastWriteTimeUtc,
                                        IsRecoveryItem = true
                                    });

                }
            }
            sftp.Disconnect();
            return backups;
        }

        public static HyperShell DestroyVM(HyperVisorItem hyperVisor, VirtualMachine vm)
        {
            var ssh = GetSshClientInstanceBySavedCredentials(hyperVisor);
            HyperShell hs = new();
            ssh.Connect();
            if (ssh.IsConnected)
            {
                string destroyCmd = $"vim-cmd vmsvc/destroy {vm.VmId}";
                var sh = CommandExec(ssh, destroyCmd);
                hs.Result = sh.Result;
                hs.ExitStatus = sh.ExitStatus;
                hs.Success = sh.ExitStatus == 0;
                CommandExec(ssh, $"vim-cmd vmsvc/unregister {vm.VmId}");
            }
            return hs;
        }

        public static HyperShell RestoreImage(HyperVisorItem hyperVisor, RestoreConfig config)
        {
            Debug.WriteLine(config);
            var ssh = GetSshClientInstanceBySavedCredentials(hyperVisor);
            HyperShell hs = new();
            ssh.Connect();
            if (ssh.IsConnected)
            {
                // prepare restore config
                Random random = new Random();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                string restoreCfgName = new string(Enumerable.Repeat(chars, 16).Select(s => s[random.Next(s.Length)]).ToArray());
                string restoreData = $"\"{config.ImagePath};{config.RestorePath};{config.DiskFormat};{config.NameOfRestoreVM}\"";
                // upload config
                var sftp = GetSFtpClientInstanceByConnectionInfo(ssh.ConnectionInfo);
                sftp.Connect();
                if (sftp.IsConnected)
                {
                    sftp.Create($"/tmp/{restoreCfgName}");
                    sftp.WriteAllText($"/tmp/{restoreCfgName}", restoreData);
                    sftp.Disconnect();
                    Debug.WriteLine($"cmd: {restoreData}");
                    Debug.WriteLine(CommandExec(ssh, $"ls -al /tmp").Result);
                    // restore
                    string restoreCmd = $"/opt/ghettovcb/bin/ghettoVCB-restore.sh -c /tmp/{restoreCfgName}";
                    var sh = CommandExec(ssh, restoreCmd);
                    hs.Result = sh.Result;
                    hs.ExitStatus = sh.ExitStatus;
                    hs.Success = sh.ExitStatus == 0;
                    // remove tmp config
                    CommandExec(ssh, $"rm -f /tmp/{restoreCfgName}");
                }
            }
            return hs;
        }

        public static bool DeleteImage(HyperVisorItem hyperVisor, Folder folder)
        {
            var ssh = GetSshClientInstanceBySavedCredentials(hyperVisor);

            ssh.Connect();
            if (ssh.IsConnected)
            {
                return CommandExec(ssh, $"rm -rf \"{folder.FullName}\"").ExitStatus == 0;
            }
            return false;
        }

        public static string GetGhettoConfig(HyperVisorItem hv)
        {
            string config = "";
            var client = GetSshClientInstanceBySavedCredentials(hv);
            client.Connect();
            if (client.IsConnected)
            {
                if (!IsGhettoInstalled(client))
                    InstallGhetto(client);
                string remoteCfgPath = ESXiGhettoConfigsDirectory + hv.GhettoBackupConfigFileName;
                // if config NOT exists on ESX machine - install it
                if (!IsPackageInstalled(client, remoteCfgPath))
                {
                    Debug.WriteLine($"ConfigExisting false: { remoteCfgPath}");
                    SetDefaultGhettoConfig(client.ConnectionInfo, remoteCfgPath);
                }
                var sftp = GetSFtpClientInstanceByConnectionInfo(client.ConnectionInfo);
                if (!sftp.IsConnected)
                    sftp.Connect();
                if (sftp.IsConnected)
                {
                    using (Stream stream = new MemoryStream())
                    {
                        sftp.DownloadFile(remoteCfgPath, stream);
                        // convert to string
                        stream.Position = 0;
                        StreamReader reader = new StreamReader(stream);
                        config = reader.ReadToEnd();
                        stream.Close();
                    }
                }
                sftp.Disconnect();
                client.Disconnect();
            }
            return config;
        }

        public static HyperShell BackupVM(HyperVisorItem hv, VirtualMachine vm)
        {
            HyperShell hs = new();
            var client = GetSshClientInstanceBySavedCredentials(hv);
            client.Connect();
            if (client.IsConnected)
            {
                if (!IsGhettoInstalled(client))
                    InstallGhetto(client);
                string remoteCfgPath = ESXiGhettoConfigsDirectory + hv.GhettoBackupConfigFileName;
                // if config NOT exists on ESX machine - install it
                bool isInstalledConfig = false;
                if (!IsPackageInstalled(client, remoteCfgPath))
                {
                    Debug.WriteLine($"ConfigExisting false: { remoteCfgPath}");
                    isInstalledConfig = SetDefaultGhettoConfig(client.ConnectionInfo, remoteCfgPath);
                }
                else
                    isInstalledConfig = true;

                if (isInstalledConfig)
                {
                    string vmname = vm.Name.Replace("\"", "\\\"");
                    var cmd = $"/opt/ghettovcb/bin/ghettoVCB.sh -m \"{vmname}\" -g {remoteCfgPath}";
                    var run = CommandExec(client, cmd);
                    hs.Result = run.Result;
                    hs.ExitStatus = run.ExitStatus;
                    hs.Success = run.ExitStatus == 0;
                }
            }
            return hs;
        }

        public static bool SetDefaultGhettoConfig(ConnectionInfo connectionInfo, string remotePath)
        {
            var sftp = GetSFtpClientInstanceByConnectionInfo(connectionInfo);
            string localDefaultConfig = Path.Combine(AssetsDirectory, "VMBackuperGhettoBackup.conf");
            if (!sftp.IsConnected)
                sftp.Connect();
            if (sftp.IsConnected && File.Exists(localDefaultConfig))
            {
                sftp.Create(remotePath);
                sftp.WriteAllBytes(remotePath, File.ReadAllBytes(localDefaultConfig));
                sftp.Disconnect();
                return true;
            }
            else
                sftp.Disconnect();
            return false;
        }

        public static List<VirtualMachine> GetVmList(SshClient client)
        {
            List<VirtualMachine> vmList = new List<VirtualMachine>();
            if (!client.IsConnected)
                client.Connect();
            if (client.IsConnected)
            {
                if (IsPackageInstalled(client, "vim-cmd"))
                {
                    var getVmList = CommandExec(client, $"vim-cmd vmsvc/getallvms");
                    var sr = new StringReader(getVmList.Result);

                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var match = Regex.Match(line, @"^(?<vmid>\d+)\s+(?<name>.*)\[(?<file>.+?)\.vmx\s+(?<guestos>.+?)\s+(?<version>.+?)\s+(?<annotation>.+?)$");
                        if (match.Success)
                        {
                            string vmid = match.Groups["vmid"].Value;
                            string name = match.Groups["name"].Value;
                            string file = match.Groups["file"].Value;
                            string guestos = match.Groups["guestos"].Value;
                            string version = match.Groups["version"].Value;
                            string annotation = match.Groups["annotation"].Value;
                            var vm = new VirtualMachine()
                            {
                                VmId = Int32.Parse(vmid),
                                Name = name.Trim(),
                                File = "[" + file + ".vmx",
                                GuestOS = guestos.Trim(),
                                Version = version.Trim(),
                                Annotation = annotation.Trim()
                            };
                            vmList.Add(vm);
                        }
                    }
                }
                client.Disconnect();
            }
            return vmList;
        }

        public static bool IsGhettoInstalled(SshClient client)
        {
            if (!client.IsConnected)
                client.Connect();
            if (client.IsConnected)
                return (IsPackageInstalled(client, "/opt/ghettovcb/bin/ghettoVCB.sh") && IsPackageInstalled(client, "/opt/ghettovcb/bin/ghettoVCB-restore.sh"));
            else
                return false;
        }

        public static bool InstallGhetto(SshClient client)
        {
            if (!client.IsConnected)
                client.Connect();
            if (client.IsConnected)
            {
                string scriptPath = Path.Combine(ScriptsDirectory, "vghetto-ghettoVCB.vib");
                var sftp = GetSFtpClientInstanceByConnectionInfo(client.ConnectionInfo);
                string remotePath = "/tmp/vghetto-ghettoVCB.vib";
                if (!sftp.IsConnected)
                    sftp.Connect();
                if (sftp.IsConnected && File.Exists(scriptPath))
                {
                    sftp.Create(remotePath);
                    sftp.WriteAllBytes(remotePath, File.ReadAllBytes(scriptPath));
                    var install = CommandExec(client, $"localcli software vib install -v {remotePath} -f");
                    if (install.ExitStatus == 0)
                    {
                        sftp.Disconnect();
                        return true;
                    }
                }
            }
            return false;
        }

        public SshConnection InitializeSshConnection()
        {
            var conn = new SshConnection();
            if (HyperVisorHasDefaultConnectionAccess())
            {
                conn.Success = true;
                conn.ConnectionType = SshConnectionType.DEFAULTCONNECTION;
                _sshConnectionType = SshConnectionType.DEFAULTCONNECTION;
                return conn;
            }
            else if (HyperVisorHasKeyboardInteractiveAccess())
            {
                conn.Success = true;
                conn.ConnectionType = SshConnectionType.KEYBOARD_INTERACTIVE;
                _sshConnectionType = SshConnectionType.KEYBOARD_INTERACTIVE;
            }
            return conn;
        }

        public bool RegisterAuthKeysOnServer()
        {
            _sshClient = GetSshClientInstance();
            if (!_sshClient.IsConnected)
                _sshClient.Connect();
            if (_sshClient.IsConnected)
            {
                // authorized_keys store paths:
                // for vmware is path: /etc/ssh/keys-root/authorized_keys
                // for default linux is path: ~/.ssh/authorized_keys
                // ssh-keygen -N '{passPhrase}' -f /tmp/{remoteKeyFileName} -t rsa <<<y
                var sshKeygenPackage = DetectSshKeygenCommand(_sshClient);
                if (sshKeygenPackage.isESXiHost)
                {
                    // check existing dir and create if needed
                    // this NOT modified existing files
                    CommandExec(_sshClient, $"mkdir -p /etc/ssh/keys-root && touch /etc/ssh/keys-root/authorized_keys");
                    // register key
                    CommandExec(_sshClient, $"cat /tmp/{_remoteKeyFileNamePub} >> /etc/ssh/keys-root/authorized_keys");
                }
                else
                {
                    // check existing dir and create if needed
                    // this NOT modified existing files
                    CommandExec(_sshClient, $"mkdir -p ~/.ssh && touch ~/.ssh/authorized_keys");
                    // register key
                    CommandExec(_sshClient, $"cat /tmp/{_remoteKeyFileNamePub} >> ~/.ssh/authorized_keys");
                }
                // clean the system - delete temp files
                CommandExec(_sshClient, $"rm /tmp/{_remoteKeyFileNamePub} & rm /tmp/{_remoteKeyFileName}");
                // disconnect on finish
                _sshClient.Disconnect();
                return true;
            }
            return false;
        }

        public static bool RemovePublicKeysFromServerAuth(HyperVisorItem hyperVisorItem)
        {
            _sshClient = GetSshClientInstanceBySavedCredentials(hyperVisorItem);
            if (!_sshClient.IsConnected)
                _sshClient.Connect();
            if (_sshClient.IsConnected)
            {
                // get the content of authorized_keys
                var sshKeygenPackage = DetectSshKeygenCommand(_sshClient);
                SshCommand sc;
                string PublicKeyFileNamePath = Path.Combine(PrivateKeysDirectory, hyperVisorItem.PublicKeyFileName);
                string publiKey = File.ReadAllText(PublicKeyFileNamePath);
                string replacedAuthorized_keys = string.Empty;
                // if ESXi mashine, then edit file in location
                // for ESXi is path: /etc/ssh/keys-root/authorized_keys
                // for default linux is path: ~/.ssh/authorized_keys
                string existingAuthorized_keys = string.Empty;
                if (sshKeygenPackage.isESXiHost)
                {
                    sc = CommandExec(_sshClient, "cat /etc/ssh/keys-root/authorized_keys");
                    existingAuthorized_keys = sc.Result;
                    replacedAuthorized_keys = Regex.Replace(existingAuthorized_keys.Replace(publiKey, string.Empty), @"\n+", "\n");
                    // replace existing Public Key in "authorized_keys" with empty
                    // write the replaced file content
                    sc = CommandExec(_sshClient, $"{{ echo '{replacedAuthorized_keys}'> /etc/ssh/keys-root/authorized_keys; }}");
                }
                else
                {
                    sc = CommandExec(_sshClient, "cat ~/.ssh/authorized_keys");
                    existingAuthorized_keys = sc.Result;
                    replacedAuthorized_keys = Regex.Replace(existingAuthorized_keys.Replace(publiKey, string.Empty), @"\n+", "\n");
                    // replace existing Public Key in "authorized_keys" with empty
                    // write the replaced file content
                    CommandExec(_sshClient, $"{{ echo '{replacedAuthorized_keys}'> ~/.ssh/authorized_keys; }}");
                }

                // remove HV ghetto config
                CommandExec(_sshClient, $"rm -f {ESXiGhettoConfigsDirectory + hyperVisorItem.GhettoBackupConfigFileName}");
                return true;
            }
            return false;
        }

        public static SshClient GetSshClientInstanceBySavedCredentials(HyperVisorItem hyperVisorItem)
        {
            string PrivateKeyFileNamePath = Path.Combine(PrivateKeysDirectory, hyperVisorItem.PrivateKeyFileName);
            var pk = new PrivateKeyFile(PrivateKeyFileNamePath, hyperVisorItem.PrivateKeyPassPhrase);
            var keyFiles = new[] { pk };
            var methods = new List<AuthenticationMethod>();
            methods.Add(new PasswordAuthenticationMethod(hyperVisorItem.UserName, hyperVisorItem.PrivateKeyPassPhrase));
            methods.Add(new PrivateKeyAuthenticationMethod(hyperVisorItem.UserName, keyFiles));

            var connInfo = new ConnectionInfo(
                hyperVisorItem.HostName,
                hyperVisorItem.Port,
                hyperVisorItem.UserName,
                methods.ToArray());
            return new SshClient(connInfo);
        }

        public static SftpClient GetSFtpClientInstanceBySavedCredentials(HyperVisorItem hyperVisorItem)
        {
            string PrivateKeyFileNamePath = Path.Combine(PrivateKeysDirectory, hyperVisorItem.PrivateKeyFileName);
            var pk = new PrivateKeyFile(PrivateKeyFileNamePath, hyperVisorItem.PrivateKeyPassPhrase);
            var keyFiles = new[] { pk };
            var methods = new List<AuthenticationMethod>();
            methods.Add(new PasswordAuthenticationMethod(hyperVisorItem.UserName, hyperVisorItem.PrivateKeyPassPhrase));
            methods.Add(new PrivateKeyAuthenticationMethod(hyperVisorItem.UserName, keyFiles));

            var connInfo = new ConnectionInfo(
                hyperVisorItem.HostName,
                hyperVisorItem.Port,
                hyperVisorItem.UserName,
                methods.ToArray());
            return new SftpClient(connInfo);
        }

        public static SftpClient GetSFtpClientInstanceByConnectionInfo(ConnectionInfo connectionInfo)
        {
            return new SftpClient(connectionInfo);
        }

        public bool GenerateRSAKeys(string passPhrase = "")
        {
            _sshClient = GetSshClientInstance();
            if (!_sshClient.IsConnected)
                _sshClient.Connect();
            if (_sshClient.IsConnected)
            {
                var sshKeygenPackage = DetectSshKeygenCommand(_sshClient);
                if (sshKeygenPackage.isExists)
                {
                    // generate RSA keys by "ssh-keygen"
                    SshCommand sc;
                    if (sshKeygenPackage.isESXiHost)
                    {
                        // generate temporary keys - private + pub
                        // RSA encryption - support not everywhere
                        //sc = _sshClient.CreateCommand($"rm -f /tmp/{_remoteKeyFileName} & rm -f /tmp/{_remoteKeyFileNamePub} & {sshKeygenPackage.SshKeygenCmd} -N '{passPhrase}' -f /tmp/{_remoteKeyFileName} -t rsa");
                        // PEM encryption
                        CommandExec(_sshClient, $"rm -f /tmp/{_remoteKeyFileName} & rm -f /tmp/{_remoteKeyFileNamePub} & {sshKeygenPackage.SshKeygenCmd} -N '{passPhrase}' -f /tmp/{_remoteKeyFileName} -m pem");
                    }
                    // else for others Linux mashines... may be need in the future =)
                    else
                    {
                        // generate temporary keys - private + pub
                        // RSA encryption - support not everywhere
                        //sc = _sshClient.CreateCommand($"rm -f /tmp/{_remoteKeyFileName} & rm -f /tmp/{_remoteKeyFileNamePub} & {sshKeygenPackage.SshKeygenCmd} -N '{passPhrase}' -f /tmp/{_remoteKeyFileName} -t rsa");
                        // PEM encryption
                        CommandExec(_sshClient, $"rm -f /tmp/{_remoteKeyFileName} & rm -f /tmp/{_remoteKeyFileNamePub} & {sshKeygenPackage.SshKeygenCmd} -N '{passPhrase}' -f /tmp/{_remoteKeyFileName} -m pem");
                    }

                    // save content of keys
                    // private
                    sc = CommandExec(_sshClient, $"cat /tmp/{_remoteKeyFileName}");
                    _privateKeyContent = sc.Result;
                    // public
                    sc = CommandExec(_sshClient, $"cat /tmp/{_remoteKeyFileNamePub}");
                    _pubKeyContent = sc.Result;
                    _sshClient.Disconnect();
                    return true;
                }
            }
            return false;
        }

        public bool HyperVisorHasKeyboardInteractiveAccess()
        {
            try
            {
                _sshClient = new SshClient(_keyboardConnectionInfo);
                _sshClient.Connect();
                var sc = CommandExec(_sshClient, $"echo {_checkResult}");
                _sshClient.Disconnect();
                // if client is connected AND has CLI access
                return sc.Result.Contains(_checkResult);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"HyperVisorHasKeyboardInteractiveAccess: {e.Message}");
                return false;
            }
        }

        public bool HyperVisorHasDefaultConnectionAccess()
        {
            try
            {
                _sshClient = new SshClient(_defaultConnectionInfo);
                _sshClient.Connect();
                var sc = CommandExec(_sshClient, $"echo {_checkResult}");
                _sshClient.Disconnect();
                // if client is connected AND has CLI access
                return sc.Result.Contains(_checkResult);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"HyperVisorHasDefaultConnectionAccess: {e.Message}");
                return false;
            }
        }

        //##################################################################################################
        // PRIVATE zone
        //##################################################################################################

        private static string GetFileContentB64Encoded(string path)
        {
            string fcontent = string.Empty;
            try { fcontent = File.ReadAllText(path); }
            catch (Exception) { }
            return Base64Encode(fcontent);
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        private SshClient GetSshClientInstance()
        {
            switch (_sshConnectionType)
            {
                case SshConnectionType.DEFAULTCONNECTION:
                    _sshClient = new SshClient(_defaultConnectionInfo);
                    break;
                case SshConnectionType.KEYBOARD_INTERACTIVE:
                    _sshClient = new SshClient(_keyboardConnectionInfo);
                    //_sshClient.KeepAliveInterval = TimeSpan.FromSeconds(60);
                    break;
                default:
                    break;
            }
            return _sshClient;
        }

        private static bool IsPackageInstalled(SshClient client, string package)
        {
            if (!client.IsConnected)
                client.Connect();
            if (client.IsConnected)
            {
                var cmd = CommandExec(client, $"type {package} >/dev/null 2>/dev/null && {{ echo '{_checkResultForInstalledPackage}'; }}");
                if (cmd.Result.Contains(_checkResultForInstalledPackage))
                    return true;
            }
            return false;
        }

        private static SshCommand CommandExec(SshClient client, string cmd)
        {
            SshCommand sc = client.CreateCommand(cmd);
            sc.Execute();
            return sc;
        }

        private static SshKeygenCommand DetectSshKeygenCommand(SshClient client)
        {
            SshKeygenCommand sshKeygen = new();
            try
            {
                // type ssh-keygen >/dev/null 2>/dev/null && { echo 'PackageHasDetected'; }
                if (!client.IsConnected) return sshKeygen;
                // detect is ESXi ??
                if (IsPackageInstalled(client, "esxcli"))
                    sshKeygen.isESXiHost = true;
                // try default variant
                string keygenCmd = "ssh-keygen";
                // if installed
                if (IsPackageInstalled(client, keygenCmd))
                {
                    sshKeygen.SshKeygenCmd = keygenCmd;
                    sshKeygen.isExists = true;
                    return sshKeygen;
                }
                // for ESXi
                // try another way -> /usr/lib/vmware/openssh/bin/ssh-keygen
                keygenCmd = "/usr/lib/vmware/openssh/bin/ssh-keygen";
                if (IsPackageInstalled(client, keygenCmd))
                {
                    sshKeygen.SshKeygenCmd = "." + keygenCmd;
                    sshKeygen.isExists = true;
                    return sshKeygen;
                }
            }
            catch (Exception) { }

            return sshKeygen;
        }
    }
}
