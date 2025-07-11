﻿using Downloader;
using MSL.langs;
using MSL.utils;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MSL
{
    /// <summary>
    /// DownloadDialog.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadDialog
    {
        public event DeleControl CloseDialog;
        public int _dialogReturn = 0; // 0未开始下载（或下载中），1下载完成，2下载取消，3下载失败
        private readonly bool enableParalle = true; // 是否启用多线程下载
        private readonly string downloadPath;
        private readonly string filename;
        private readonly string downloadurl;
        private readonly string expectedSha256;
        private readonly bool closeDirectly;
        private readonly int headerMode; // 0等于无Header，1等于MSL Downloader，2等于伪装浏览器Header
        private DownloadService downloader;
        private DispatcherTimer updateUITimer;

        public DownloadDialog(string _downloadurl, string _downloadPath, string _filename, string downloadinfo, string sha256 = "", bool _closeDirectly = false, bool _enableParalle = true, int header = 1)
        {
            InitializeComponent();
            Directory.CreateDirectory(_downloadPath);
            downloadurl = _downloadurl;
            downloadPath = _downloadPath;
            filename = _filename;
            expectedSha256 = sha256;
            closeDirectly = _closeDirectly;
            headerMode = header;
            taskinfo.Text = downloadinfo;
            enableParalle = _enableParalle;
            Task.Run(Downloader);
        }

        private void Downloader()
        {
            if (File.Exists(Path.Combine(downloadPath, filename)))
            {
                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    if (VerifyFileSHA256(Path.Combine(downloadPath, filename), expectedSha256))
                    {
                        _dialogReturn = 1;
                        Task.Run(async () =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                infolabel.Text = "文件已存在，下载完成";
                                StatusLab.Text = "下载完成";
                            });
                            await Task.Delay(1000);
                            Dispatcher.Invoke(() =>
                            {
                                Close();
                            });
                        });
                        return;
                    }
                }
            }

            var downloadOpt = new DownloadConfiguration();
            downloadOpt.RequestConfiguration.UserAgent = DownloadUA();
            if (enableParalle)
            {
                downloadOpt.ParallelDownload = true; // download parts of file as parallel or not. Default value is false
                downloadOpt.ChunkCount = ConfigStore.DownloadChunkCount; // file parts to download, default value is 1
            }
            downloader = new DownloadService(downloadOpt);
            // Provide `FileName` and `TotalBytesToReceive` at the start of each downloads
            downloader.DownloadStarted += OnDownloadStarted;

            // Provide any information about chunker downloads, 
            // like progress percentage per chunk, speed, 
            // total received bytes and received bytes array to live streaming.
            //downloader.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;

            // Provide any information about download progress, 
            // like progress percentage of sum of chunks, total speed, 
            // average speed, total received bytes and received bytes array 
            // to live streaming.
            downloader.DownloadProgressChanged += OnDownloadProgressChanged;

            // Download completed event that can include occurred errors or 
            // cancelled or download completed successfully.
            downloader.DownloadFileCompleted += OnDownloadFileCompleted;
            downloader.DownloadFileTaskAsync(downloadurl, downloadPath + "\\" + filename);

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                Dispatcher.Invoke(() =>
                {
                    if (StatusLab.Text.Contains("加载中"))
                        StatusLab.Text = "加载中（若长时间无响应，请取消重试或使用代理）";
                });
            });
        }

        private void OnDownloadStarted(object sender, DownloadStartedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PauseBtn.IsEnabled = true;
                infolabel.Text = string.Format(LanguageManager.Instance["DownloadDialog_OnDownloadStarted"], e.TotalBytesToReceive / 1024 / 1024);
                // 初始化DispatcherTimer
                updateUITimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                updateUITimer.Tick += UpdateUITick;
                updateUITimer.Start();
                StatusLab.Text = "下载中……";
            });
        }

        // 下载完成的事件
        private void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    PauseBtn.IsEnabled = false;
                    updateUITimer?.Stop();
                });
            }
            catch { Console.WriteLine("Stop UITimer Failed"); }
            if (e.Cancelled || _dialogReturn == 2)
            {
                Dispatcher.Invoke(() =>
                {
                    infolabel.Text = LanguageManager.Instance["DownloadDialog_DownloadCancel"];
                    StatusLab.Text = LanguageManager.Instance["DownloadDialog_DownloadCancel"];
                    button1.Content = LanguageManager.Instance["Close"];
                    try
                    {
                        File.Delete(downloadPath + "\\" + filename);
                    }
                    catch { Console.WriteLine("Delete File Failed"); }
                });
            }
            else
            {
                if (e.Error != null || !File.Exists(downloadPath + "\\" + filename))
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusLab.Text = "下载中……";
                        pbar.Value = 0;
                        Thread thread = new Thread(DownloadFile);
                        thread.Start();
                    });
                    /* 此处已转移至备用下载方案（Thread(DownloadFile)）失败时的执行逻辑
                    if (closeDirectly)
                    {
                        Thread.Sleep(1000);
                        Dispatcher.Invoke(Close);
                    }
                    */
                    return;
                }

                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    //有传入sha256，进行校验
                    if (VerifyFileSHA256(downloadPath + "\\" + filename, expectedSha256) == false)
                    {
                        //失败
                        _dialogReturn = 3;
                        Dispatcher.Invoke(() =>
                        {
                            button1.Content = LanguageManager.Instance["Close"];
                            infolabel.Text = LanguageManager.Instance["DownloadDialog_CheckIntegrityFailed"];
                            try
                            {
                                File.Delete(downloadPath + "\\" + filename);
                            }
                            catch { }
                        });
                        if (closeDirectly)
                        {
                            Thread.Sleep(1000);
                            Dispatcher.Invoke(Close);
                        }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    infolabel.Text = LanguageManager.Instance["DownloadDialog_DownloadComplete"];
                    pbar.Value = 100;
                });

                _dialogReturn = 1;
            }
            Thread.Sleep(1000);
            Dispatcher.Invoke(Close);
        }

        private string DownloadUA()
        {
            if (headerMode == 1)
            {
                return "MSLTeam-MSL/" + ConfigStore.MSLVersion + " (Downloader)";
            }
            else if (headerMode == 2)
            {
                return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
            }
            return null;
        }

        private void DownloadFile()
        {
            // 使用Task异步执行下载任务
            Task.Run(() =>
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(downloadurl);
                    request.UserAgent = DownloadUA();
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        long totalBytes = response.ContentLength;
                        using (Stream responseStream = response.GetResponseStream())
                        using (FileStream fileStream = new FileStream(Path.Combine(downloadPath, filename), FileMode.Create))
                        {
                            byte[] buffer = new byte[1024];
                            int bytesRead;
                            long totalDownloadedByte = 0;
                            // 创建Progress<T>来报告进度
                            var progress = new Progress<int>(percent =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    if (pbar != null)
                                    {
                                        pbar.Value = percent;
                                        infolabel.Text = string.Format(LanguageManager.Instance["DownloadDialog_Mode2_Downloading"], percent);
                                    }
                                });
                            });

                            while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                if (_dialogReturn == 2) break;
                                fileStream.Write(buffer, 0, bytesRead);
                                totalDownloadedByte += bytesRead;
                                // 计算并报告进度
                                int percentComplete = (int)(totalDownloadedByte * 100 / totalBytes);
                                ((IProgress<int>)progress).Report(percentComplete);
                            }
                        }
                    }
                    // 下载完成后更新UI
                    Dispatcher.Invoke(() =>
                    {
                        if (_dialogReturn == 2 && File.Exists(Path.Combine(downloadPath, filename)))
                        {
                            File.Delete(Path.Combine(downloadPath, filename));
                            infolabel.Text = LanguageManager.Instance["DownloadDialog_DownloadCancel"];
                            StatusLab.Text = LanguageManager.Instance["DownloadDialog_DownloadCancel"];
                            button1.Content = LanguageManager.Instance["Close"];
                            PauseBtn.IsEnabled = false;
                        }
                        else
                        {
                            _dialogReturn = 1;
                            infolabel.Text = LanguageManager.Instance["DownloadDialog_DownloadComplete"];
                        }
                    });
                }
                catch (Exception ex)
                {
                    _dialogReturn = 3;
                    // 异常处理
                    Dispatcher.Invoke(() =>
                    {
                        button1.Content = LanguageManager.Instance["Close"];
                        infolabel.Text = LanguageManager.Instance["DownloadDialog_DownloadFailed"];
                        StatusLab.Text = LanguageManager.Instance["DownloadDialog_DownloadFailed"] + "\n" + ex.Message;
                    });
                }
                Thread.Sleep(1000);
            }).ContinueWith(t =>
            {
                if (_dialogReturn != 3 || closeDirectly)
                {
                    // 关闭对话框
                    Dispatcher.Invoke(Close);
                }
            });
        }

        private long receivedBytes;
        private long totalBytesToReceive;
        private double progressPercentage;
        private double bytesPerSecondSpeed;

        private void UpdateUITick(object sender, EventArgs e)
        {
            // 更新UI的方法
            if (pbar != null && infolabel != null)
            {
                infolabel.Text = string.Format(LanguageManager.Instance["DownloadDialog_Downloading"],
                    receivedBytes / 1024 / 1024,
                    totalBytesToReceive / 1024 / 1024,
                    progressPercentage.ToString("F2"),
                    (bytesPerSecondSpeed / 1024 / 1024).ToString("F2"));
                pbar.Value = progressPercentage;
            }
        }

        private void OnDownloadProgressChanged(object sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            // 更新变量，供UpdateUITick使用
            receivedBytes = e.ReceivedBytesSize;
            totalBytesToReceive = e.TotalBytesToReceive;
            progressPercentage = e.ProgressPercentage;
            bytesPerSecondSpeed = e.BytesPerSecondSpeed;
        }


        public static class DispatcherHelper
        {
            [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
            public static void DoEvents()
            {
                DispatcherFrame frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrames), frame);
                try { Dispatcher.PushFrame(frame); }
                catch (InvalidOperationException) { }
            }
            private static object ExitFrames(object frame)
            {
                ((DispatcherFrame)frame).Continue = false;
                return null;
            }
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PauseBtn.Content.ToString() == "暂停")
            {
                downloader.Pause();
                PauseBtn.Content = "继续";
                StatusLab.Text = "已暂停";
            }
            else
            {
                downloader.Resume();
                PauseBtn.Content = "暂停";
                StatusLab.Text = "下载中……";
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (button1.Content.ToString() == LanguageManager.Instance["Close"])
            {
                Close();
            }
            else
            {
                downloader?.CancelAsync();
                _dialogReturn = 2;
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    Dispatcher.Invoke(() =>
                    {
                        if (StatusLab.Text.ToString() != LanguageManager.Instance["DownloadDialog_DownloadCancel"])
                        {
                            StatusLab.Text = "取消任务中，请稍等……\n双击取消按钮可强制关闭此对话框（不推荐）";
                        }
                    });
                });
            }
        }

        private void button1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (button1.Content.ToString() == LanguageManager.Instance["Close"])
            {
                Close();
            }
            else
            {
                downloader?.CancelAsync();
                _dialogReturn = 2;
                Close();
            }
        }

        // 用于校验sha256的函数
        public bool VerifyFileSHA256(string filePath, string expectedHash)
        {
            using (FileStream stream = File.OpenRead(filePath)) //文件流
            {
                SHA256Managed sha = new SHA256Managed();
                byte[] hash = sha.ComputeHash(stream);
                string calculatedHash = BitConverter.ToString(hash).Replace("-", string.Empty);

                return string.Equals(calculatedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void Close()
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimation scaleDownX = new DoubleAnimation(1, 1.1, TimeSpan.FromSeconds(0.15));
            DoubleAnimation scaleDownY = new DoubleAnimation(1, 1.1, TimeSpan.FromSeconds(0.15));
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.15));

            storyboard.Children.Add(scaleDownX);
            storyboard.Children.Add(scaleDownY);
            storyboard.Children.Add(fadeOut);

            if (Template.FindName("contentPresenter", this) is ContentPresenter contentPresenter)
            {
                Storyboard.SetTarget(scaleDownX, contentPresenter);
                Storyboard.SetTarget(scaleDownY, contentPresenter);
                Storyboard.SetTarget(fadeOut, contentPresenter);

                Storyboard.SetTargetProperty(scaleDownX, new PropertyPath("RenderTransform.ScaleX"));
                Storyboard.SetTargetProperty(scaleDownY, new PropertyPath("RenderTransform.ScaleY"));
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

                storyboard.Completed += (s, a) =>
                {
                    Visibility = Visibility.Collapsed;
                    if (downloader != null)
                    {
                        downloader.DownloadStarted -= OnDownloadStarted;
                        downloader.DownloadProgressChanged -= OnDownloadProgressChanged;
                        downloader.DownloadFileCompleted -= OnDownloadFileCompleted;
                    }
                    CloseDialog();
                };

                storyboard.Begin();
            }
        }
    }
}
