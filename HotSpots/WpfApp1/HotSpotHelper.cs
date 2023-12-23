using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Devices.WiFiDirect;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace WpfApp1
{
    public class HotSpotHelper
    {
        static WiFiDirectAdvertisementPublisher _publisher;
        static WiFiDirectConnectionListener _listener;
        static NetworkOperatorTetheringManager _networkOperatorTetheringManager;
        static ConnectionProfile _firstConnectProfile = NetworkInformation.GetConnectionProfiles()?.FirstOrDefault();
        private static ConnectionProfile _connectionProfile;
        public static ConnectionProfile ConnectionProfile => _connectionProfile;


        /// <summary>
        /// 网络共享管理对象
        /// </summary>
        public static NetworkOperatorTetheringManager NetworkOperatorTetheringManager
        {
            get
            {
                if (_networkOperatorTetheringManager == null)
                {
                    try
                    {
                        _connectionProfile = NetworkInformation.GetInternetConnectionProfile();
                        if (_connectionProfile == null)
                        {
                            _connectionProfile = _firstConnectProfile ?? NetworkInformation.GetConnectionProfiles()?.FirstOrDefault();
                        }
                        if (_connectionProfile != null)
                        {
                            _networkOperatorTetheringManager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(_connectionProfile);
                        }
                    }
                    catch (Exception ex)
                    {

                    }

                }
                return _networkOperatorTetheringManager;
            }
        }

        /// <summary>
        /// 热点名称
        /// </summary>
        public static string Ssid
        {
            get
            {
                return NetworkOperatorTetheringManager?.GetCurrentAccessPointConfiguration().Ssid;
            }
        }

        /// <summary>
        /// 热点密码
        /// </summary>
        public static string Pwd
        {
            get
            {
                return NetworkOperatorTetheringManager?.GetCurrentAccessPointConfiguration()?.Passphrase;
            }
        }
        /// <summary>
        /// 监听WiFi链接
        /// </summary>
        private static WiFiDirectAdvertisementPublisher WiFiDirectPublisher
        {
            get
            {
                if (_publisher == null)
                    _publisher = new WiFiDirectAdvertisementPublisher();
                return _publisher;
            }
        }

        private static WiFiDirectConnectionListener WiFiDirectListener
        {
            get
            {
                if (_listener == null)
                    _listener = new WiFiDirectConnectionListener();
                return _listener;
            }
        }

        public static uint GetConnectedClients()
        {
            uint uiRet = 0;

            if (NetworkOperatorTetheringManager != null)
            {
                uiRet = NetworkOperatorTetheringManager.ClientCount;
            }
            return uiRet;
        }


        public static bool GetWifiState()
        {
            bool bRet = false;
            if (NetworkOperatorTetheringManager != null)
            {
                if (NetworkOperatorTetheringManager.TetheringOperationalState == TetheringOperationalState.On)
                {
                    bRet = true;
                }
            }
            return bRet;
        }

        /// <summary>
        /// 开始热点的监听事件
        /// </summary>
        /// <param name="ssid"></param>
        /// <param name="pass"></param>
        /// <returns></returns>
        public async Task<bool> OperationListen(string ssid, string pass)
        {
            try
            {

                await StopShareWiFi();

                WiFiDirectPublisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;
                WiFiDirectPublisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;
                WiFiDirectPublisher.Advertisement.LegacySettings.IsEnabled = true;

                WiFiDirectPublisher.Advertisement.LegacySettings.Ssid = ssid;
                var creds = new Windows.Security.Credentials.PasswordCredential();
                creds.Password = pass;
                WiFiDirectPublisher.Advertisement.LegacySettings.Passphrase = creds;
                WiFiDirectPublisher.Start();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// 开启共享网络
        /// </summary>
        /// <param name="ssid"></param>
        /// <param name="pass"></param>
        /// <returns></returns>
        public async static Task<bool> StartShareWiFiAsync(string ssid, string pass)
        {
            bool _isSuccessful = false;
            try
            {

                if (!GetWifiState())
                {
                    _networkOperatorTetheringManager = null;
                    NetworkOperatorTetheringAccessPointConfiguration notapc = new NetworkOperatorTetheringAccessPointConfiguration();
                    notapc.Ssid = ssid;
                    notapc.Passphrase = pass;
                    if (NetworkOperatorTetheringManager == null) return false;
                    await NetworkOperatorTetheringManager.ConfigureAccessPointAsync(notapc);
                    var result = await NetworkOperatorTetheringManager.StartTetheringAsync();
                    if (result.Status == TetheringOperationStatus.Success)
                    {
                        _isSuccessful = true;
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return _isSuccessful;
        }

        public static void UpdateWiFiConfigAsync(string ssid, string pass, Action<bool> resultAction)
        {

            try
            {
                if (NetworkOperatorTetheringManager != null)
                {
                    NetworkOperatorTetheringAccessPointConfiguration notapc = new NetworkOperatorTetheringAccessPointConfiguration();
                    notapc.Ssid = ssid;
                    notapc.Passphrase = pass;

                    NetworkOperatorTetheringManager.ConfigureAccessPointAsync(notapc).Completed += (s, e) =>
                    {
                        bool _isSuccessful = false;
                        if (e == AsyncStatus.Completed)
                            _isSuccessful = true;
                        else
                        {
                            //更改热点失败  
                        }
                        resultAction?.Invoke(_isSuccessful);
                    };

                }
            }
            catch (Exception ex)
            {
                resultAction?.Invoke(false);
            }

        }


        /// <summary>
        /// 关闭WiFi
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> StopShareWiFi()
        {
            try
            {
                if (NetworkOperatorTetheringManager.TetheringOperationalState == TetheringOperationalState.On)
                {
                    var result = await NetworkOperatorTetheringManager.StopTetheringAsync();
                    if (result.Status == TetheringOperationStatus.Success)
                    {
                        _networkOperatorTetheringManager = null;
                        return true;
                    }
                    await Task.Delay(500);
                }
                int dcount = 0;
                while (NetworkOperatorTetheringManager.TetheringOperationalState == TetheringOperationalState.InTransition || NetworkOperatorTetheringManager.TetheringOperationalState == TetheringOperationalState.On)
                {
                    if (dcount++ == 2)
                        throw new Exception("三次循环关闭失败");
                    if (NetworkOperatorTetheringManager.TetheringOperationalState == TetheringOperationalState.On)
                    {
                        var result = await NetworkOperatorTetheringManager.StopTetheringAsync();
                        if (result.Status == TetheringOperationStatus.Success)
                            break;
                        await Task.Delay(300);
                    }
                    await Task.Delay(1000);
                }

                _networkOperatorTetheringManager = null;

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 外部设备链接WiFi监听事件
        /// </summary>
        /// <param name = "sender" ></ param >
        /// < param name="connectionEventArgs"></param>
        private static void OnConnectionRequested(WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs connectionEventArgs)
        {
            try
            {
                WiFiDirectConnectionRequest request = connectionEventArgs.GetConnectionRequest(); 
            }
            catch (Exception ex)
            { 
            }
        }


        /// <summary>
        /// 热点开启节能模式(不关闭热点)
        /// </summary>
        /// <returns></returns>
        public static async Task StartPowerSave()
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\icssvc\Settings", RegistryKeyPermissionCheck.ReadWriteSubTree, System.Security.AccessControl.RegistryRights.FullControl))
                    {
                        var data = key?.GetValue("PeerlessTimeoutEnabled");
                        //byte[] byteData = (byte[])data;
                        if (data == null || (int)data == 1)
                        {
                            Process p = new Process();
                            p.StartInfo.FileName = "cmd.exe";
                            p.StartInfo.UseShellExecute = false;//是否使用操作系统shell启动
                            p.StartInfo.RedirectStandardInput = true;//接受来自调用程序的输入信息
                            p.StartInfo.RedirectStandardOutput = true;//由调用程序获取输出信息
                            p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
                            p.StartInfo.CreateNoWindow = true;//不显示程序窗口
                            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            p.Start();//启动程序

                            string strCMD = "net stop \"icssvc\" & REG ADD \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\icssvc\\Settings\" /V PeerlessTimeoutEnabled /T REG_DWORD /D 0 /F & net start \"icssvc\" & net stop \"icssvc\" & REG ADD \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\icssvc\\Settings\" /V PeerlessTimeoutEnabled /T REG_DWORD /D 0 /F & net start \"icssvc\" ";//this is argument;
                                                                                                                                                                                                                                                                                                                                                                                         //向cmd窗口发送输入信息
                            p.StandardInput.WriteLine(strCMD + "&exit");
                            p.StandardInput.AutoFlush = true;

                            //string output = p.StandardOutput.ReadToEnd();
                            //等待程序执行完退出进程
                            p.WaitForExit();
                            p.Close();

                        }
                    }
                }
                catch (Exception ex)
                {
                }

            });

        }

    }

}
