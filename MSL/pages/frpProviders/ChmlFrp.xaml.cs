﻿using MSL.controls;
using MSL.i18n;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace MSL.pages.frpProviders
{
    /// <summary>
    /// ChmlFrp.xaml 的交互逻辑
    /// </summary>
    public partial class ChmlFrp : Page
    {
        string ChmlFrpApiUrl = "https://panel.chmlfrp.cn";
        public ChmlFrp()
        {
            InitializeComponent();
        }

        //使用token登录
        private async void userTokenLogin_Click(object sender, RoutedEventArgs e)
        {
            string token;
            token = await Shows.ShowInput(Window.GetWindow(this), "请输入Chml账户Token", "", true);
            Task.Run(() => verifyUserToken(token));
        }

        //账号密码
        private async void userLogin_Click(object sender, RoutedEventArgs e)
        {
            string frpUser, frpPassword;
           frpUser = await Shows.ShowInput(Window.GetWindow(this), "请输入ChmlFrp的账户名/邮箱/QQ号");
            frpPassword = await Shows.ShowInput(Window.GetWindow(this), "请输入密码","", true);
            Task.Run(() => getUserToken(frpUser, frpPassword));
        }

        //注册一个可爱的账户
        private void userRegister_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://panel.chmlfrp.cn/register");
        }


        //异步登录，获取到用户token
        private void getUserToken(string user,string pwd)
        {
            string response = Functions.Post("api/login.php", 2, $"username={user}&password={pwd}", ChmlFrpApiUrl);
            var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
            if (jsonResponse.ContainsKey("code"))
            {
                if (jsonResponse["code"].ToString() == "200")
                {
                    string token = jsonResponse["token"].ToString();
                    //这里就拿到token了
                    Task.Run(() => GetFrpList(token));
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        Shows.ShowMsgDialog(Window.GetWindow(this), "登陆失败！" + jsonResponse["message"].ToString(), LanguageManager.Instance["Dialog_Err"]);
                    });
                    
                }
            }
            else
            {
                if (jsonResponse.ContainsKey("error"))
                {
                    Dispatcher.Invoke(() =>
                    {

                        Shows.ShowMsgDialog(Window.GetWindow(this), "登陆失败！\n" + jsonResponse["error"].ToString(), LanguageManager.Instance["Dialog_Err"]);
                    });
                    }
                    
            }
            
            
        }

        //直接token登录，那么验证下咯~
        private void verifyUserToken(string userToken)
        {
            string response = Functions.Get($"api/userinfo.php?usertoken={userToken}", ChmlFrpApiUrl);
            var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
            if (jsonResponse.ContainsKey("userid"))
            {
                //这里就拿到token了
                Task.Run(() => GetFrpList(userToken));
            }
            else
            {
                if (jsonResponse.ContainsKey("error"))
                {
                    Dispatcher.Invoke(() =>
                    {
                        Shows.ShowMsgDialog(Window.GetWindow(this), "Token登陆失败！\n可以尝试账号密码登录！\n" + jsonResponse["error"].ToString(), LanguageManager.Instance["Dialog_Err"]);
                    });
                    }

            }
        }

        public class TunnelInfo
        {
            public string ID { get; set; }
            public string Type { get; set; }
            public string LPort { get; set; }
            public string RPort { get; set; }
            public string LIP { get; set; }
            public string Name { get; set; }
            public string Node { get; set; }
            public string Addr { get; set; }
        }

        //登录成功了，然后就是获取隧道,丢到ui去
        private void GetFrpList(String token )
        {
            //处理ui界面交接
            Dispatcher.Invoke(() =>
            {
                MainGrid.Visibility = Visibility.Visible;
                LoginGrid.Visibility = Visibility.Collapsed;
            });
            //获取userinfo
            var jsonUserInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(Functions.Get($"api/userinfo.php?usertoken={token}", ChmlFrpApiUrl));
            Dispatcher.Invoke(() =>
            {
                UserInfo.Text= $"用户ID:{jsonUserInfo["userid"]}  用户名:{jsonUserInfo["username"]}\n邮箱:{jsonUserInfo["email"]}  会员类型:{jsonUserInfo["usergroup"]}\n隧道数:{jsonUserInfo["tunnelstate"]}/{jsonUserInfo["tunnel"]}";
            });

            //获取隧道
            ObservableCollection<TunnelInfo> tunnels = new ObservableCollection<TunnelInfo>();
            Dispatcher.Invoke(() =>
            {
                FrpList.ItemsSource = tunnels;
            });
            string response = Functions.Get($"api/usertunnel.php?token={token}", ChmlFrpApiUrl);
            try
            {
                var jsonArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(response);
                foreach (var item in jsonArray)
                {
                    Dispatcher.Invoke(() =>
                    {
                        tunnels.Add(new TunnelInfo { Name = $"{item["name"]}", Node = $"{item["node"]}" ,ID= $"{item["id"]}" ,Type= $"{item["type"]}",LIP= $"{item["localip"]}",LPort= $"{item["nport"]}",RPort= $"{item["dorp"]}" ,Addr = $"{item["ip"]}" });
                    });
                }
            }
            catch (JsonSerializationException)
            {
                Dispatcher.Invoke(() =>
                {
                    Shows.ShowMsgDialog(Window.GetWindow(this), "建议创建一个哦~", "您似乎没有隧道");
                });
            }

        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            MainGrid.Visibility = Visibility.Collapsed;
            LoginGrid.Visibility = Visibility.Visible;
        }

        private void FrpList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox.SelectedItem is TunnelInfo selectedTunnel)
            {
                TunnelInfo_Text.Text=$"隧道名:{selectedTunnel.Name}\n隧道ID:{selectedTunnel.ID}\n协议:{selectedTunnel.Type}\n地域:{selectedTunnel.Node}\n远程端口:{selectedTunnel.RPort}";
                LocalIp.Text=selectedTunnel.LIP;
                LocalPort.Text=selectedTunnel.LPort;
            }
        }
    }
}
