﻿using Downloader;
using System;
using System.ComponentModel;
using System.IO;
using System.Security.Permissions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MSL
{
    /// <summary>
    /// DownloadWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadWindow : Window
    {
        //DownLoadFile dlf = new DownLoadFile();
        public static int downloadthread = 8;
        public static string downloadPath;
        public static string filename;
        public static string downloadurl;
        bool ifStop = false;
        //DispatcherTimer timer1 = new DispatcherTimer();
        //DispatcherTimer timer2 = new DispatcherTimer();
        //static Thread thread;
        public DownloadWindow(string _downloadurl, string _downloadPath, string _filename, string downloadinfo)
        {
            InitializeComponent();

            downloadurl = _downloadurl;
            downloadPath= _downloadPath;
            filename = _filename;

            taskinfo.Text = downloadinfo;
            ifStop = false;
            Thread thread = new Thread(Downloader);
            thread.Start();
        }
        void Downloader()
        {
            var downloadOpt = new DownloadConfiguration()
            {
                ChunkCount = downloadthread, // file parts to download, default value is 1
                ParallelDownload = true // download parts of file as parallel or not. Default value is false
            };
            var downloader = new DownloadService(downloadOpt);
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
            downloader.DownloadFileTaskAsync(downloadurl, downloadPath + @"\" + filename);
            while (ifStop != true)
            {
                Thread.Sleep(1000);
            }
            downloader.CancelAsync();
        }

        private void OnDownloadStarted(object sender, DownloadStartedEventArgs e)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                infolabel.Text = "获取下载地址……大小：" + e.TotalBytesToReceive / 1024 / 1024 + "MB";
            });
        }
        private void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (ifStop == true)
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    infolabel.Text = "取消成功！";
                    try
                    {
                        File.Delete(downloadPath + @"\" + filename);
                    }
                    catch { }
                });
            }
            else
            {
                this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    infolabel.Text = "下载完成！";
                });
            }
            Thread.Sleep(1000);
            downloadurl = null;
            //Close();
            this.Dispatcher.Invoke(new Action(
    delegate
    {
        Close();
    })
    );
        }
        private void OnDownloadProgressChanged(object sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                infolabel.Text = "已下载：" + e.ReceivedBytesSize / 1024 / 1024 + "MB/" + e.TotalBytesToReceive / 1024 / 1024 + "MB" + " 进度：" + e.ProgressPercentage.ToString("f2") + "%" + " 速度：" + (e.BytesPerSecondSpeed / 1024 / 1024).ToString("f2") + "MB/s";
                pbar.Value = e.ProgressPercentage;
            });
            //Thread.Sleep(1000);
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

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            //Downloader.CancelAsync();
            ifStop = true;
        }

        private void button1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ifStop = true;
            Close();
        }
    }
}