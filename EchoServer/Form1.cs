using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;

namespace EchoServer
{
    public partial class Form1 : Form
    {
        private System.Net.Sockets.TcpListener SvrListener;
        private System.Net.Sockets.TcpClient UserClient;
        private System.Net.Sockets.NetworkStream NetStm;
        private System.Threading.CancellationTokenSource cts;
        private bool EndFlag = false;
        private bool IsClientCn = false;

        private System.Windows.Forms.RichTextBox RichTextBox1;
        private System.Windows.Forms.CheckBox CheckBox1;
        private System.Windows.Forms.Button ButtonConec;
        private System.Windows.Forms.Button ButtonCnCancel;
        private System.Windows.Forms.Button ButtonAsyncCancel;
        private System.Windows.Forms.Button ButtonExit;
        public Form1()
        {
            InitializeComponent();
            FormDesignSetting();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Location = new Point(250, 100);

            ButtonCnCancel.Enabled = false;
            RichTextBox1.AppendText("クライアント接続ボタンを押しクライアント接続を有効にして下さい。。" + "\r\n");
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (IsClientCn)
            {
                NetStm.Close();
                UserClient.Close();
                SvrListener.Stop();
            }
        }

        private void ButtonExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void ButtonConec_Click(object sender, EventArgs e)
        {
            SvrListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 3000);
            cts = new System.Threading.CancellationTokenSource();

            ButtonConec.Enabled = false;
            ButtonCnCancel.Enabled = true;
            RichTextBox1.AppendText("サーバーはクライアントの接続を待っています。" + "\r\n");

            await ClientMatchi();
            if (EndFlag)
            {
                IsClientCn = false;
                RichTextBox1.AppendText("クライアント接続をキャンセルしました。" + "\r\n");
                return;
            }
            else
            {
                IsClientCn = true;
                RichTextBox1.AppendText("クライアントが接続されました。" + "\r\n");
            }

            NetStm = UserClient.GetStream();
            ButtonCnCancel.Enabled = false;
            ButtonAsyncCancel.Enabled = true;
            RichTextBox1.AppendText("クライアントからの電文を待機しています。" + "\r\n");

            try
            {
                var tk1 = Task.Run(() =>
                {
                    RecSend(cts.Token);
                });
                await tk1;

            }
            catch (System.OperationCanceledException ex)
            {
                ButtonConec.Enabled = true;
                ButtonAsyncCancel.Enabled = false;
                RichTextBox1.AppendText("送受信を強制終了しました。" + "\r\n");
            }
        }

        private void ButtonCnCancel_Click(object sender, EventArgs e)
        {
            SvrListener.Stop();
            ButtonConec.Enabled = true;
            ButtonCnCancel.Enabled = false;
            ButtonAsyncCancel.Enabled = false;
            EndFlag = true;
        }

        private void ButtonAsyncCancel_Click(object sender, EventArgs e)
        {
            //非同期処理をキャンセルします。
            cts.Cancel();
        }

        //非同期で接続待ち
        private async System.Threading.Tasks.Task ClientMatchi()
        {
            SvrListener.Start();
            var tk1 = Task.Run(() =>
            {
                try
                {
                    UserClient = SvrListener.AcceptTcpClient();
                }
                catch (SocketException ex)
                {
                    //SvrListener.Stop()メソッドが実行されたら例外が発生する。
                    this.Invoke((Action)(() => { RichTextBox1.AppendText(ex.Message + "\r\n"); }));
                }
            });
            await tk1;
        }

        //非同期電文送受信
        private async void RecSend(System.Threading.CancellationToken ct)
        {
            //クライアントから送られたデータを受信する
            System.Text.Encoding enc = System.Text.Encoding.UTF8;
            bool disconnected = false;
            System.IO.MemoryStream ms;
            byte[] resBytes = new byte[255]; // = { };
            string MsgRece = "";
            string MsgSend = "";

            while (EndFlag == false)
            {
                ms = new System.IO.MemoryStream();
                while (NetStm.DataAvailable == false)
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException ex)
                    {
                        //cts.Cancel()メソッドが実行されたら例外が発生する。
                        this.Invoke((Action)(() =>
                        {
                            ButtonConec.Enabled = true;
                            ButtonAsyncCancel.Enabled = false;
                            RichTextBox1.AppendText("送受信を強制終了しました。2" + "\r\n");
                        }));
                        if (IsClientCn)
                        {
                            NetStm.Close();
                            UserClient.Close();
                            SvrListener.Stop();
                        }
                        return;
                    }
                    await Task.Delay(100);
                }
                //データの一部を受信する。
                //NetworkStream.Read()メソッドを実行して読み取れるデータが無い場合(実は0を返さない様だ)ここでスレッドはブロックされる。
                //これを実行する前にデータの受信が1以上になるまで待機してから実行する事。
                //このブロックはNetworkstream.ReadTimeoutの設定値により決まるが既定値は-1(無限)なのでデータが送られるまでブロックされる。
                //DataAvailableプロパティでTrue(受信バッファ有り)をチェックしてからならReadTimeoutのタイムアウトは事実上発生しない。
                //ReadTimeoutプロパティはNetworkstream.Read()メソッドを実行した後、読み取り可能データが入るまでの時間の様だ。
                int resSize = NetStm.Read(resBytes, 0, resBytes.Length);
                //Readが0を返した時はクライアントが切断したと判断
                if (resSize == 0)
                {
                    disconnected = true;
                    return;
                }
                //受信したデータを蓄積する
                ms.Write(resBytes, 0, resSize);


                //受信したデータを文字列に変換
                string resMsg = enc.GetString(ms.ToArray());
                ms.Close();
                MsgRece = resMsg.Replace("\r", "");
                this.Invoke((Action)(() => { RichTextBox1.AppendText(MsgRece + "\r\n"); }));

                if (!disconnected)
                {
                    //クライアントにデータを送信する
                    //クライアントに送信する文字列を作成
                    string sendMsg = "";
                    if (resMsg.StartsWith("ST R") || resMsg.StartsWith("RS R"))
                    {
                        sendMsg = "OK" + "\r" + "\n";
                    }
                    else
                    {
                        this.Invoke((Action)(() =>
                        {
                            if (MsgRece == "RD R4601")
                            {
                                if (CheckBox1.Checked)
                                {
                                    sendMsg = "1" + "\r" + "\n";
                                }
                                else
                                {
                                    sendMsg = "0" + "\r" + "\n";
                                }
                            }
                            else
                            {
                                sendMsg = "NG" + "\r" + "\n";
                            }
                        }));

                        //文字列をByte型配列に変換
                        byte[] sendBytes = enc.GetBytes(sendMsg);
                        //データを送信する
                        NetStm.Write(sendBytes, 0, sendBytes.Length);
                        MsgSend = sendMsg.Replace("\r", "");

                        this.Invoke((Action)(() => { RichTextBox1.AppendText(sendMsg + "\r\n"); }));
                    }
                }
            }

        }

        private void FormDesignSetting()
        {
            this.RichTextBox1 = new System.Windows.Forms.RichTextBox();
            this.CheckBox1 = new System.Windows.Forms.CheckBox();
            this.ButtonConec = new System.Windows.Forms.Button();
            this.ButtonCnCancel = new System.Windows.Forms.Button();
            this.ButtonAsyncCancel = new System.Windows.Forms.Button();
            this.ButtonExit = new System.Windows.Forms.Button();
            // 
            // RichTextBox1
            // 
            this.RichTextBox1.Location = new System.Drawing.Point(15, 30);
            this.RichTextBox1.Name = "RichTextBox1";
            this.RichTextBox1.Size = new System.Drawing.Size(350, 720);
            this.RichTextBox1.TabIndex = 0;
            this.RichTextBox1.Text = "";
            // 
            // CheckBox1
            // 
            this.CheckBox1.AutoSize = true;
            this.CheckBox1.Location = new System.Drawing.Point(390, 150);
            this.CheckBox1.Name = "CheckBox1";
            this.CheckBox1.Size = new System.Drawing.Size(108, 19);
            this.CheckBox1.TabIndex = 1;
            this.CheckBox1.Text = "サーバーの状態１";
            this.CheckBox1.UseVisualStyleBackColor = true;
            // 
            // ButtonConec
            // 
            this.ButtonConec.Location = new System.Drawing.Point(390, 30);
            this.ButtonConec.Name = "ButtonConec";
            this.ButtonConec.Size = new System.Drawing.Size(115, 40);
            this.ButtonConec.TabIndex = 2;
            this.ButtonConec.Text = "クライアント接続";
            this.ButtonConec.UseVisualStyleBackColor = true;
            this.ButtonConec.Click += new System.EventHandler(this.ButtonConec_Click);
            // 
            // ButtonCnCancel
            // 
            this.ButtonCnCancel.Location = new System.Drawing.Point(520, 30);
            this.ButtonCnCancel.Name = "ButtonCnCancel";
            this.ButtonCnCancel.Size = new System.Drawing.Size(145, 40);
            this.ButtonCnCancel.TabIndex = 3;
            this.ButtonCnCancel.Text = "クライアント接続キャンセル";
            this.ButtonCnCancel.UseVisualStyleBackColor = true;
            this.ButtonCnCancel.Click += new System.EventHandler(this.ButtonCnCancel_Click);
            // 
            // ButtonAsyncCancel
            // 
            this.ButtonAsyncCancel.Location = new System.Drawing.Point(390, 85);
            this.ButtonAsyncCancel.Name = "ButtonAsyncCancel";
            this.ButtonAsyncCancel.Size = new System.Drawing.Size(115, 40);
            this.ButtonAsyncCancel.TabIndex = 4;
            this.ButtonAsyncCancel.Text = "通信強制終了";
            this.ButtonAsyncCancel.UseVisualStyleBackColor = true;
            this.ButtonAsyncCancel.Click += new System.EventHandler(this.ButtonAsyncCancel_Click);
            // 
            // ButtonExit
            // 
            this.ButtonExit.Location = new System.Drawing.Point(550, 85);
            this.ButtonExit.Name = "ButtonExit";
            this.ButtonExit.Size = new System.Drawing.Size(115, 40);
            this.ButtonExit.TabIndex = 5;
            this.ButtonExit.Text = "閉じる";
            this.ButtonExit.UseVisualStyleBackColor = true;
            this.ButtonExit.Click += new System.EventHandler(this.ButtonExit_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 761);
            this.Controls.Add(this.ButtonExit);
            this.Controls.Add(this.ButtonAsyncCancel);
            this.Controls.Add(this.ButtonCnCancel);
            this.Controls.Add(this.ButtonConec);
            this.Controls.Add(this.CheckBox1);
            this.Controls.Add(this.RichTextBox1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form1";
            this.Text = "ソケット通信・エコーサーバー・サンプル";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
        }

    }
}