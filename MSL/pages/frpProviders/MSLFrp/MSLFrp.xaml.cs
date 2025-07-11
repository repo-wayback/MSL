﻿using MSL.utils;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MSL.pages.frpProviders.MSLFrp
{
    /// <summary>
    /// MSLFrp.xaml 的交互逻辑
    /// </summary>
    public partial class MSLFrp : Page
    {
        private MSLFrpProfile FrpProfile=new MSLFrpProfile();

        public MSLFrp()
        {
            InitializeComponent();
        }

        private bool isInit = false;
        private async void Page_Loaded(object sender, EventArgs e)
        {
            if (isInit)
                return;

            isInit = true;

            // 获取Token并尝试登录
            var token = string.IsNullOrEmpty(MSLFrpApi.UserToken)
                ? Config.Read("MSLUserAccessToken")?.ToString()
                : MSLFrpApi.UserToken;

            if (!string.IsNullOrEmpty(token))
            {
                // 登录并获取用户信息
                var loginSuccess = await AttemptLogin(token);
                if (!loginSuccess)
                {
                    ShowLoginControl();
                    return;  // 登录失败，返回
                }

                // 获取隧道列表
                await GetTunnelList();
                return;
            }

            ShowLoginControl();
        }

        private void ShowLoginControl()
        {
            // 显示登录页面
            MSLFrpLogin loginControl = new MSLFrpLogin();
            loginControl.LoginSuccess += async delegate(JObject UserInfo)
            {
                LoginControl.Visibility = Visibility.Collapsed;
                MainCtrl.Visibility = Visibility.Visible;
                UpdateUserInfo(UserInfo);
                await GetTunnelList();
            };
            LoginControl.Content = loginControl;
            LoginControl.Visibility = Visibility.Visible;
            MainCtrl.Visibility = Visibility.Collapsed;
        }

        // 封装登录操作
        private async Task<bool> AttemptLogin(string token)
        {
            MagicDialog magicDialog = new MagicDialog();
            magicDialog.ShowTextDialog(Window.GetWindow(this), "登录中……");

            var (Code, Msg,UserInfo) = await MSLFrpApi.UserLogin(token);

            magicDialog.CloseTextDialog();

            if (Code != 200)
            {
                MagicShow.ShowMsgDialog(Window.GetWindow(this), "登陆失败！\n" + Msg, "错误");
                return false;
            }

            // 解析用户信息并更新UI
            UpdateUserInfo(UserInfo);
            MagicFlowMsg.ShowMessage("登录成功！", 1);
            return true;
        }

        // 更新用户信息UI
        private void UpdateUserInfo(JObject JsonUserInfo)
        {
            int userLevel = Functions.GetCurrentUnixTimestamp() < (long)JsonUserInfo["data"]["outdated"]
                ? int.Parse((string)JsonUserInfo["data"]["user_group"])
                : 0;

            string userGroup = userLevel == 0 ? "普通用户" : userLevel == 1 ? "高级用户" : userLevel == 2 ? "超级用户" : "管理员";
            string outDate = userLevel == 0 ? string.Empty : "\n到期时间：{0}";

            UserInfo.Content = $"用户名：{JsonUserInfo["data"]["name"]}\n" +
                $"用户组：{userGroup} \n" +
                $"可创建隧道数：{JsonUserInfo["data"]["maxTunnelCount"]}" +
                string.Format(outDate, Functions.ConvertUnixTimeSeconds((long)JsonUserInfo["data"]["outdated"]));
        }


        private async Task GetTunnelList()
        {
            //获取隧道
            var (Code, TunnelList, Msg) = await MSLFrpApi.GetTunnelList();
            if (Code != 200)
            {
                MagicShow.ShowMsgDialog(Window.GetWindow(this), "获取失败！\n" + Msg, "错误");
                return;
            }
            FrpList.ItemsSource = TunnelList;
        }

        private async Task GetNodeList()
        {
            //获取隧道
            var (Code, _NodeList, Msg) = await MSLFrpApi.GetNodeList();
            if (Code != 200)
            {
                MagicShow.ShowMsgDialog(Window.GetWindow(this), "获取失败！\n" + Msg, "错误");
                return;
            }
            NodeList.ItemsSource = _NodeList;
        }

        private async void MainCtrl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
            {
                return;
            }
            if (!ReferenceEquals(e.OriginalSource, this.MainCtrl))
            {
                return;
            }
            switch (MainCtrl.SelectedIndex)
            {
                case 0:
                    await GetTunnelList();
                    break;
                case 1:
                    await GetNodeList();
                    Create_Name.Text = Functions.RandomString("MSL_", 6);
                    break;
                case 2:
                    UserCenterFrame.Content = FrpProfile;
                    break;
            }
        }

        

        //显示隧道信息
        private void FrpList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox.SelectedItem is MSLFrpApi.TunnelInfo selectedTunnel)
            {
                TunnelInfo_Text.Content = $"#{selectedTunnel.ID} {selectedTunnel.Name}\n" +
                    $"本地IP：{selectedTunnel.LIP} 本地端口：{selectedTunnel.LPort}\n远程端口: {selectedTunnel.RPort}" + $"\n隧道状态: {(selectedTunnel.Online ? "在线" : "离线")}";
            }
        }

        //确定 输出config
        private async void OKBtn_Click(object sender, RoutedEventArgs e)
        {
            var listBox = FrpList;
            if (listBox.SelectedItem is MSLFrpApi.TunnelInfo selectedTunnel)
            {
                OKBtn.IsEnabled = false;
                var (Code, Content) = await Task.Run(() => MSLFrpApi.GetTunnelConfig(selectedTunnel.ID));
                OKBtn.IsEnabled = true;
                if (Code != 200)
                {
                    MagicShow.ShowMsgDialog(Window.GetWindow(this), "出现错误！" + Content, "错误");
                    return;
                }
                //输出配置文件
                if (Config.WriteFrpcConfig(0, $"MSLFrp - {selectedTunnel.Name} | {selectedTunnel.Node}", Content) == true)
                {
                    await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), "映射配置成功，请您点击“启动内网映射”以启动映射！", "信息");
                    Window.GetWindow(this).Close();
                }
                else
                {
                    await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), "配置输出失败！", "错误");
                }
            }
            else
            {
                await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), "您似乎没有选择任何隧道！", "错误");
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await GetTunnelList();
        }

        private async void Del_Tunnel_Click(object sender, RoutedEventArgs e)
        {
            var listBox = FrpList;
            if (listBox.SelectedItem is MSLFrpApi.TunnelInfo selectedTunnel)
            {
                Del_Tunnel.IsEnabled = false;
                var (Code, Msg) = await MSLFrpApi.DelTunnel(selectedTunnel.ID);
                Del_Tunnel.IsEnabled = true;
                await GetTunnelList();
                if (Code != 200)
                {
                    MagicShow.ShowMsgDialog(Window.GetWindow(this), Msg, "错误");
                    return;
                }
                MagicShow.ShowMsgDialog(Window.GetWindow(this), Msg, "提示");
            }
            else
            {
                await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), "您似乎没有选择任何隧道！", "错误");
            }

        }

        private void NodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NodeList.SelectedItem is MSLFrpApi.NodeInfo selectedNode)
            {
                Create_RemotePort.Text = Functions.GenerateRandomNumber(selectedNode.MinPort, selectedNode.MaxPort).ToString();
                if (selectedNode.KCP == 1)
                {
                    KCPProtocol.IsEnabled = true;
                }
                else
                {
                    KCPProtocol.IsEnabled = false;
                    KCPProtocol.IsChecked = false;
                }
            }
        }

        private async void Create_OKBtn_Click(object sender, RoutedEventArgs e)
        {
            var listBox = NodeList;
            if (listBox.SelectedItem is MSLFrpApi.NodeInfo selectedNode)
            {
                Create_OKBtn.IsEnabled = false;
                try
                {
                    bool kcpProtocol = false;
                    if (KCPProtocol.IsChecked == true)
                        kcpProtocol = true;
                    var (Code, Msg) = await MSLFrpApi.CreateTunnel(selectedNode.ID, Create_Name.Text, Create_Protocol.Text, "Create By MSL Client", Create_LocalIP.Text, int.Parse(Create_LocalPort.Text), int.Parse(Create_RemotePort.Text), kcpProtocol);
                    if (Code == 200)
                    {
                        await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), $"{Msg}\n隧道名称：{Create_Name.Text}\n远程端口： {Create_RemotePort.Text}", "成功");
                        //显示main页面
                        MainCtrl.SelectedIndex = 0;
                    }
                    else
                    {
                        await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), "创建失败！请尝试更换隧道名称/节点！\n" + Msg, "错误");
                    }
                }
                catch
                {
                    await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), "创建失败！请检查输入是否正确！", "错误");
                }
                finally
                {
                    Create_OKBtn.IsEnabled = true;
                }
            }
            else
            {
                await MagicShow.ShowMsgDialogAsync(Window.GetWindow(this), "您似乎没有选择任何节点！", "错误");
            }
        }

        private void GenerateRandomPort_Click(object sender, RoutedEventArgs e)
        {
            var listBox = NodeList;
            if (listBox.SelectedItem is MSLFrpApi.NodeInfo selectedNode)
            {
                Create_RemotePort.Text = Functions.GenerateRandomNumber(selectedNode.MinPort, selectedNode.MaxPort).ToString();
            }
            else
            {
                MagicShow.ShowMsgDialog(Window.GetWindow(this), "您似乎没有选择任何节点！", "错误");
            }
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            //显示登录页面
            ShowLoginControl();
            MSLFrpApi.UserToken = string.Empty;
            Config.Remove("MSLUserAccessToken");
            FrpProfile = new MSLFrpProfile();
        }
    }

    internal class MSLStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value switch
            {
                1 => "在线",
                _ => "离线"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class MSLUDPConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value switch
            {
                1 => "UDP：支持",
                _ => "UDP：不支持"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class MSLKCPConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((int)value == 1)
            {
                return "KCP：支持";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class MSLKCPVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value switch
            {
                1 => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
