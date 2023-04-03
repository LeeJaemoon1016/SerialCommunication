using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO.Ports;
using System.IO;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Forms;

namespace SerialCommunication
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Stopwatch sw;
        SerialPort serialPort = new SerialPort();
        SerialPort GMSserialPort = new SerialPort();
        string GMSrecievedData;
        string recievedData;
        string addr = "000";
        bool powerOnOff;
        bool elOnOff;
        bool vddOnOff;
        bool Connect;
        bool Cycle;
        int OnTimeSec;
        int OffTimeSec;
        FlowDocument mcFlowDoc = new FlowDocument();
        Paragraph para = new Paragraph();
        Thread thread;
        Thread TimeThread;

        // Rcv Data 저장을 위한 전역변수들 선언
        String m_tempBuffer = "";
        bool m_bStart = false;
        int m_lengthBuffer = 0;

        // Cal Test Data 비교를 위한 Cal Data Array 2개 선언
        string[] CompareCalData1 = new string[84];
        string[] CompareCalData2 = new string[84];

        // Cal Test Array 증감 변수
        int array1;
        int array2;

        // Cal Test에 실제로 쓰이는 결과값
        String realData;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            powerOnOff = false;
            elOnOff = false;
            vddOnOff = false;
            Cycle = false;
            //Console.WriteLine("The following serial ports were found:");            
            foreach (string port in ports)
            {
                //Console.WriteLine(port); // Display each port name to the console.
                cBoxComPort.Items.Add(port);
                cGMSBoxComPort.Items.Add(port);
            }
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                serialPort.PortName = cBoxComPort.Text;
                serialPort.BaudRate = Convert.ToInt32(cBoxBaudRate.Text);
                serialPort.DataBits = Convert.ToInt32(cBoxDataBits.Text);
                serialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cBoxStopBits.Text);
                serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), cBoxParityBits.Text);
                serialPort.Open(); // Open port.
                serialPort.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(serialPort_DataRecieved);

                pBar.Value = 100;

                if (serialPort.IsOpen)
                {
                    string str = "$" + addr + "SETRS";
                    string str1 = str + CalculateChecksum(str) + "\n";
                    serialPort.Write(str1);
                    Connect = true;
                }
            }
            catch (Exception err)
            {
                System.Windows.MessageBox.Show(err.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close(); // Close port.
                pBar.Value = 0;
            }
        }

        private void BtnSendData_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {

            }
        }

        private string CalculateChecksum(string dataToCalculate)
        {
            byte[] byteToCalculate = Encoding.ASCII.GetBytes(dataToCalculate);
            int checksum = 0;
            foreach (byte chData in byteToCalculate)
            {
                checksum += chData;
            }
            checksum &= 0xff;
            return checksum.ToString("X2");
        }

        private delegate void UpdateUiTextDelegate(string text);

        //시리얼 통신 Rcv Data 함수
        private void serialPort_DataRecieved(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            // Collecting the characters received to our 'buffer' (string).
            recievedData = serialPort.ReadExisting();
            MainWindow.DebugLog("[Serial] " + recievedData);

            m_lengthBuffer = recievedData.Length;
            for (int i = 0; i < m_lengthBuffer; i++)
            {
                if (recievedData[i] == '$')
                {
                    m_bStart = true;
                    m_tempBuffer = "";
                }
                if (m_bStart)
                {
                    m_tempBuffer += recievedData[i];
                    if (recievedData[i] == '\n')
                    {
                        m_bStart = false;
                        //링버퍼에 넣기
                        MainWindow.DebugLog("[Filtered] " + m_tempBuffer);

                        Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(DataWrited), m_tempBuffer);
                    }
                }
            }
        }

        // Rcv Data 가공하는 함수
        private void DataWrited(string text)
        {
            if (text.Contains("MONIA"))
            {
                try
                {
                    tBoxInData.AppendText(text);
                    tBoxInData.Select(tBoxInData.Text.Length, 0);
                    tBoxInData.ScrollToEnd();
                    MVDD_V.Content = "VDD V : " + text.Substring(9, 6);
                    MVDD_I.Content = "VDD I : " + text.Substring(15, 6);
                    MEL_V.Content = "EL V    : " + text.Substring(21, 6);
                    MEL_I.Content = "EL I    : " + text.Substring(27, 6);
                }
                catch (Exception e)
                {

                }
            }
            else if (Connect)
            {
                if (text.Contains("SETRS"))
                {
                    try
                    {
                        btn_Cycle.Background = Brushes.Red;
                        if (text.Substring(33, 1) == "0")
                        {
                            vddOnOff = false;
                            btn_vddonoff.Background = Brushes.Red;
                        }
                        if (text.Substring(33, 1) == "1")
                        {
                            vddOnOff = true;
                            btn_vddonoff.Background = Brushes.Green;
                        }
                        if (text.Substring(34, 1) == "0")
                        {
                            elOnOff = false;
                            btn_elonoff.Background = Brushes.Red;
                        }
                        if (text.Substring(34, 1) == "1")
                        {
                            elOnOff = true;
                            btn_elonoff.Background = Brushes.Green;
                        }
                        if (vddOnOff && elOnOff)
                        {
                            powerOnOff = true;
                            btn_poweronoff.Background = Brushes.Green;
                        }
                        if (!vddOnOff && !elOnOff)
                        {
                            powerOnOff = false;
                            btn_poweronoff.Background = Brushes.Red;
                        }
                        Connect = false;
                    }
                    catch (Exception e)
                    {
                        Connect = false;
                    }
                }
            }
            else if (text.Contains("SETRS"))
            {
                try
                {
                    tBoxInData.AppendText("\nADDRESS: " + addr + "\nSet Read:\nVDD V: " + text.Substring(9, 6) + " VDD I: " + text.Substring(15, 6)
                         + "\nEL V: " + text.Substring(21, 6) + " EL I: " + text.Substring(27, 6) + "\n");
                    tBoxInData.Select(tBoxInData.Text.Length, 0);
                    tBoxInData.ScrollToEnd();
                }
                catch (Exception e)
                {

                }
            }
            else if (text.Contains("SETRL"))
            {
                try
                {
                    tBoxInData.AppendText("\nADDRESS: " + addr + "\nLimit Read:\nVDD V: " + text.Substring(9, 6) + " VDD I: " + text.Substring(15, 6)
                         + "\nEL V: " + text.Substring(21, 6) + " EL I: " + text.Substring(27, 6) + "\n");
                    tBoxInData.Select(tBoxInData.Text.Length, 0);
                    tBoxInData.ScrollToEnd();
                }
                catch (Exception e)
                {

                }
            }
            else if (text.Contains("$000GTCAL"))
            {
                realData = text.Substring(12, 18);

                try
                {
                    //tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss]") + realData + "\n");
                    //tBoxInData.Select(tBoxInData.Text.Length, 0);
                    //tBoxInData.ScrollToEnd();
                    MainWindow.RcvLog(realData);

                    if (array1 < 84)
                    {
                        String Value = realData;
                        CompareCalData1[array1] = realData;
                        MainWindow.CompareLog("CompareCalData1 [" + array1 + "] : " + realData);
                        StandardLog(realData);
                        tBoxInData.AppendText(System.DateTime.Now.ToString("CompareCalData1 [" + array1 + "] : ") + realData + "\n");
                        tBoxInData.Select(tBoxInData.Text.Length, 0);
                        tBoxInData.ScrollToEnd();
                        array1++;
                    }
                    else if (array1 == 84 && array2 < 84)
                    {
                        String Value = realData;
                        CompareCalData2[array2] = realData;
                        MainWindow.CompareLog("CompareCalData2 [" + array2 + "] : " + realData);
                        StandardLog(realData);
                        tBoxInData.AppendText(System.DateTime.Now.ToString("CompareCalData2 [" + array2 + "] : ") + realData + "\n");
                        tBoxInData.Select(tBoxInData.Text.Length, 0);
                        tBoxInData.ScrollToEnd();
                        array2++;

                        if (array1 == 84 && array2 == 84 && CompareCalData2[83] != null)
                        {
                            CAL_TEST_CHECK.Background = System.Windows.Media.Brushes.White;
                            CAL_TEST_CHECK.Clear();

                            bool same = CompareCalData1.SequenceEqual(CompareCalData2);

                            if (same == true)
                            {
                                tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss] 2CH CAL DATA Same.\n"));
                                tBoxInData.Select(tBoxInData.Text.Length, 0);
                                tBoxInData.ScrollToEnd();
                                MainWindow.RcvLog("2CH POWER CAL DATA Same.\n");

                                CAL_TEST_CHECK.AppendText("OK");
                                CAL_TEST_CHECK.Background = System.Windows.Media.Brushes.Green;
                                CAL_TEST_CHECK.Foreground = System.Windows.Media.Brushes.White;
                            }
                            else
                            {
                                tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss] 2CH CAL DATA Different.\n"));
                                tBoxInData.Select(tBoxInData.Text.Length, 0);
                                tBoxInData.ScrollToEnd();
                                MainWindow.RcvLog("2CH POWER CAL DATA Different.\n");

                                CAL_TEST_CHECK.AppendText("NG");
                                CAL_TEST_CHECK.Background = System.Windows.Media.Brushes.Red;
                                CAL_TEST_CHECK.Foreground = System.Windows.Media.Brushes.White;
                            }
                            array1 = 0;
                            array2 = 0;
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                    }
                }
                catch (Exception e)
                {
                }
            }
            else
            {
                tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss]") + text + "\n");
                tBoxInData.Select(tBoxInData.Text.Length, 0);
                tBoxInData.ScrollToEnd();
            }

        }

        private void BtnPower_On(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {

                if (!powerOnOff)
                {
                    string str = "$" + addr + "ONOFF11";
                    string str1 = str + CalculateChecksum(str) + "\n";
                    serialPort.Write(str1);
                    powerOnOff = true;
                    vddOnOff = true;
                    elOnOff = true;
                    btn_poweronoff.Background = Brushes.Green;
                    btn_vddonoff.Background = Brushes.Green;
                    btn_elonoff.Background = Brushes.Green;
                }
                else
                {
                    string str = "$" + addr + "ONOFF00";
                    string str1 = str + CalculateChecksum(str) + "\n";
                    serialPort.Write(str1);
                    powerOnOff = false;
                    vddOnOff = false;
                    elOnOff = false;
                    btn_poweronoff.Background = Brushes.Red;
                    btn_vddonoff.Background = Brushes.Red;
                    btn_elonoff.Background = Brushes.Red;
                }

            }
        }

        private void BtnPower_Off(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "ONOFF00";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnPower_Set(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string data;
                string setData;
                data = "$" + addr + "SETVI" + SET_VDDV.Text + SET_VDDI.Text + SET_ELV.Text + SET_ELI.Text;
                setData = data + CalculateChecksum(data) + "\n";

                serialPort.Write(setData);
            }
        }

        private void BtnPower_LimitSet(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string data;
                string setData;
                data = "$" + addr + "LIMIT" + LIMIT_VDDV.Text + LIMIT_VDDI.Text + LIMIT_ELV.Text + LIMIT_ELI.Text;
                setData = data + CalculateChecksum(data) + "\n";

                serialPort.Write(setData);
            }
        }

        private void BtnVDD_On(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                if (!vddOnOff)
                {
                    string str = "$" + addr + "ONOFF10";
                    string str1 = str + CalculateChecksum(str) + "\n";
                    serialPort.Write(str1);
                    vddOnOff = true;
                    btn_vddonoff.Background = Brushes.Green;
                }
                else
                {
                    string str = "$" + addr + "ONOFF01";
                    string str1 = str + CalculateChecksum(str) + "\n";
                    serialPort.Write(str1);
                    vddOnOff = false;
                    btn_vddonoff.Background = Brushes.Red;
                }
            }
        }

        private void BtnVDD_Off(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "ONOFF01";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnEL_On(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                if (!elOnOff)
                {
                    string str = "$" + addr + "ONOFF01";
                    string str1 = str + CalculateChecksum(str) + "\n";
                    serialPort.Write(str1);
                    elOnOff = true;
                    btn_elonoff.Background = Brushes.Green;
                }
                else
                {
                    string str = "$" + addr + "ONOFF10";
                    string str1 = str + CalculateChecksum(str) + "\n";
                    serialPort.Write(str1);
                    elOnOff = false;
                    btn_elonoff.Background = Brushes.Red;
                }
            }
        }

        private void BtnEL_Off(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "ONOFF10";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        //Send Data 로그 남기기
        public static void SendLog(string str)
        {
            // 현재 위치 경로
            string currentDirectoryPath = Environment.CurrentDirectory.ToString();
            // Logs 디렉토리 경로(현재 경로에 Logs라는 디렉토리 경로 합치기)
            string DirPath = System.IO.Path.Combine(currentDirectoryPath, "Logs");
            // Logs\Log_yyyyMMdd.log 형식의 로그 파일 경로
            string FilePath = DirPath + @"\Send_Log_" + DateTime.Today.ToString("yyyyMMdd") + ".log";
            // Logs 디렉토리 정보
            DirectoryInfo di = new DirectoryInfo(DirPath);
            // 로그 파일 경로 정보
            FileInfo fi = new FileInfo(FilePath);
            try
            {
                // Logs 디렉토리가 없을 경우 생성
                if (!di.Exists) Directory.CreateDirectory(DirPath);
                // 오류 메세지 생성
                string error_string = string.Format("{0}: \t{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), str);

                // Log 파일이 존재할 경우와 존재하지 않을 경우로 나누어서 진행
                if (!fi.Exists)
                {
                    using (StreamWriter sw = new StreamWriter(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
            }
            catch
            {
            }
        }

        //Rcv Data 로그 남기기
        public static void RcvLog(string str)
        {
            // 현재 위치 경로
            string currentDirectoryPath = Environment.CurrentDirectory.ToString();
            // Logs 디렉토리 경로(현재 경로에 Logs라는 디렉토리 경로 합치기)
            string DirPath = System.IO.Path.Combine(currentDirectoryPath, "Logs");
            // Logs\Log_yyyyMMdd.log 형식의 로그 파일 경로
            string FilePath = DirPath + @"\Rcv_Log_" + DateTime.Today.ToString("yyyyMMdd") + ".log";
            // Logs 디렉토리 정보
            DirectoryInfo di = new DirectoryInfo(DirPath);
            // 로그 파일 경로 정보
            FileInfo fi = new FileInfo(FilePath);
            try
            {
                // Logs 디렉토리가 없을 경우 생성
                if (!di.Exists) Directory.CreateDirectory(DirPath);
                // 오류 메세지 생성
                string error_string = string.Format("{0}: \t{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), str);

                // Log 파일이 존재할 경우와 존재하지 않을 경우로 나누어서 진행
                if (!fi.Exists)
                {
                    using (StreamWriter sw = new StreamWriter(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
            }
            catch
            {
            }
        }

        //디버그 로그 남기기
        public static void DebugLog(string str)
        {
            // 현재 위치 경로
            string currentDirectoryPath = Environment.CurrentDirectory.ToString();
            // Logs 디렉토리 경로(현재 경로에 Logs라는 디렉토리 경로 합치기)
            string DirPath = System.IO.Path.Combine(currentDirectoryPath, "Logs");
            // Logs\Log_yyyyMMdd.log 형식의 로그 파일 경로
            string FilePath = DirPath + @"\Debug_Log_" + DateTime.Today.ToString("yyyyMMdd") + ".log";
            // Logs 디렉토리 정보
            DirectoryInfo di = new DirectoryInfo(DirPath);
            // 로그 파일 경로 정보
            FileInfo fi = new FileInfo(FilePath);
            try
            {
                // Logs 디렉토리가 없을 경우 생성
                if (!di.Exists) Directory.CreateDirectory(DirPath);
                // 오류 메세지 생성
                string error_string = string.Format("{0}: \t{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), str);

                // Log 파일이 존재할 경우와 존재하지 않을 경우로 나누어서 진행
                if (!fi.Exists)
                {
                    using (StreamWriter sw = new StreamWriter(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
            }
            catch
            {
            }
        }

        // 두 측정 결과 비교 로그 남기기
        public static void CompareLog(string str)
        {
            // 현재 위치 경로
            string currentDirectoryPath = Environment.CurrentDirectory.ToString();
            // Logs 디렉토리 경로(현재 경로에 Logs라는 디렉토리 경로 합치기)
            string DirPath = System.IO.Path.Combine(currentDirectoryPath, "Logs");
            // Logs\Log_yyyyMMdd.log 형식의 로그 파일 경로
            string FilePath = DirPath + @"\Compare_Log_" + DateTime.Today.ToString("yyyyMMdd") + ".log";
            // Logs 디렉토리 정보
            DirectoryInfo di = new DirectoryInfo(DirPath);
            // 로그 파일 경로 정보
            FileInfo fi = new FileInfo(FilePath);
            try
            {
                // Logs 디렉토리가 없을 경우 생성
                if (!di.Exists) Directory.CreateDirectory(DirPath);
                // 오류 메세지 생성
                string error_string = string.Format("{0}: \t{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), str);

                // Log 파일이 존재할 경우와 존재하지 않을 경우로 나누어서 진행
                if (!fi.Exists)
                {
                    using (StreamWriter sw = new StreamWriter(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
            }
            catch
            {
            }
        }

        // 기준 데이터 로그 남기기
        public void StandardLog(string str)
        {
            // 현재 위치 경로
            string currentDirectoryPath = Environment.CurrentDirectory.ToString();
            // Logs 디렉토리 경로(현재 경로에 Logs라는 디렉토리 경로 합치기)
            string DirPath = System.IO.Path.Combine(currentDirectoryPath, "EEPROM_DATA");
            // Logs\Log_yyyyMMdd.log 형식의 로그 파일 경로
            string FilePath = DirPath + @"\" + SET_EL_NO.Text + ".EEPROM";
            // Logs 디렉토리 정보
            DirectoryInfo di = new DirectoryInfo(DirPath);
            // 로그 파일 경로 정보
            FileInfo fi = new FileInfo(FilePath);
            try
            {
                // Logs 디렉토리가 없을 경우 생성
                if (!di.Exists) Directory.CreateDirectory(DirPath);
                // 오류 메세지 생성
                string error_string = string.Format("{0}", str);

                // Log 파일이 존재할 경우와 존재하지 않을 경우로 나누어서 진행
                if (!fi.Exists)
                {
                    using (StreamWriter sw = new StreamWriter(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
                else
                {
                    // 로그 파일 줄 수 센다.
                    var lineCount = File.ReadLines(FilePath).Count();

                    // 파일이 있고, 로그 파일 줄이 84줄이면 삭제하고 새로 쓴다.
                    if (lineCount == 84)
                    {
                        File.Delete(FilePath);
                    }

                    using (StreamWriter sw = File.AppendText(FilePath))
                    {
                        sw.WriteLine(error_string);
                        sw.Close();
                    }
                }
            }
            catch
            {
            }
        }

        // Cal READ Test 함수용 플래그 선언
        int m_flag = 0;
        // Cal READ Test용 버튼 만듦.
        private void BtnCalRead_Test(object sender, RoutedEventArgs e)
        {
            if (m_flag == 0)
            {
                m_flag = 1;
                // Send Data 함수 딜레이를 주기 위한 별도 태스크로 분리
                Task.Run(() =>
            {
                if (serialPort.IsOpen)
                {
                    if (vddOnOff)
                    {
                        int i;
                        int j;
                        int k;

                        for (i = 0; i <= 3; i++)
                        {
                            for (j = 0; j <= 2; j++)
                            {
                                for (k = 0; k <= 9; k++)
                                {
                                    if ((i * 100) + (j * 10) == 121 || (i * 100) + (j * 10) == 221 || (i * 100) + (j * 10) == 321 || (j * 10) + k == 21)
                                    {
                                        break;
                                    }
                                    string str = "$" + addr + "GTCAL" + i + j + k;
                                    string str1 = str + CalculateChecksum(str);
                                    string str2 = str + CalculateChecksum(str) + "\n";
                                    serialPort.Write(str2);
                                    Thread.Sleep(100);

                                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        //tBoxInData.AppendText("Send Data : " + str2);
                                        //tBoxInData.Select(tBoxInData.Text.Length, 0);
                                        //tBoxInData.ScrollToEnd();
                                    });

                                    MainWindow.SendLog("Send Data " + (i) + (j) + (k) + " : " + str1);
                                }
                            }
                        }
                    }
                    else
                    {

                    }
                }
            });
            }
            m_flag = 0;
        }

        private void BtnDelay_Set(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string data;
                string setData;
                data = "$" + addr + "DELAY" + ed_Delay.Text;
                setData = data + CalculateChecksum(data) + "\n";

                serialPort.Write(setData);
            }
        }

        private void BtnERROR_State(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "GTERR";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnInternal_ADC(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "GIADC";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnRESET(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "RESET";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnMonitoring(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "MONIA";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnRead_Limit(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "SETRL";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnRead_SET(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                string str = "$" + addr + "SETRS";

                string str1 = str + CalculateChecksum(str) + "\n";
                //serialPort.Write(tBoxOutData.Text);
                serialPort.Write(str1);

            }
        }

        private void BtnAddress_Set(object sender, RoutedEventArgs e)
        {
            addr = ed_Address.Text;
            DataWrited("ADDRESS - " + addr + " Set\n");
        }

        private void BtnCycle(object sender, RoutedEventArgs e)
        {
            if (!Cycle)
            {
                if (Int32.Parse(Ontime.Text) > 0 && Int32.Parse(OffTime.Text) > 0)
                {
                    Cycle = true;
                    OnTimeSec = Int32.Parse(Ontime.Text);
                    OffTimeSec = Int32.Parse(OffTime.Text);
                    btn_Cycle.Background = Brushes.Green;
                    Time.Content = "";
                    sw = new Stopwatch();
                    sw.Start();
                    thread = new Thread(OnOffThread);
                    thread.Start();
                    TimeThread = new Thread(AgingTimeThread);
                    TimeThread.Start();
                    SetButtonColor(0);
                    tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss]") + " Aging Start\n");
                    tBoxInData.Select(tBoxInData.Text.Length, 0);
                    tBoxInData.ScrollToEnd();

                }

            }
            else if (Cycle)
            {
                sw.Stop();
                thread.Interrupt();
                thread.Abort();
                thread.Join();
                TimeThread.Abort();
                TimeThread.Join();
                SetButtonColor(1);
                string str = "$" + addr + "SETRS";
                string str1 = str + CalculateChecksum(str) + "\n";
                serialPort.Write(str1);
                Connect = true;
                Cycle = false;
                btn_Cycle.Background = Brushes.Red;
                tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss]") + " Aging End\n");
                tBoxInData.Select(tBoxInData.Text.Length, 0);
                tBoxInData.ScrollToEnd();
            }
        }

        private void OnOffThread()
        {
            try
            {
                while (true)
                {
                    if (serialPort.IsOpen)
                    {
                        string str = "$" + addr + "ONOFF11";
                        string str1 = str + CalculateChecksum(str) + "\n";
                        serialPort.Write(str1);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                        {
                            tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss]") + " ON\n");
                            tBoxInData.Select(tBoxInData.Text.Length, 0);
                            tBoxInData.ScrollToEnd();
                        });
                        Thread.Sleep(OnTimeSec * 1000);

                        string str2 = "$" + addr + "ONOFF00";
                        string str3 = str2 + CalculateChecksum(str2) + "\n";
                        serialPort.Write(str3);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                        {
                            tBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss]") + " OFF\n");
                            tBoxInData.Select(tBoxInData.Text.Length, 0);
                            tBoxInData.ScrollToEnd();
                        });
                        Thread.Sleep(OffTimeSec * 1000);
                    }
                }

            }
            catch
            {

            }
        }

        private void AgingTimeThread()
        {
            while (true)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    Time.Content = msTostring(sw.ElapsedMilliseconds.ToString());
                });
                Thread.Sleep(1000);
            }
        }

        private string msTostring(string ms)
        {
            string data = ms;
            int time = int.Parse(data) / 1000;

            return String.Format("{0}H {1}M {2}S", time / 3600, time % 3600 / 60, time % 3600 % 60);
        }

        private void SetButtonColor(int type)
        {
            if (type == 0)
            {
                btn_SET.IsEnabled = false;
                btn_poweronoff.IsEnabled = false;
                btn_vddonoff.IsEnabled = false;
                btn_elonoff.IsEnabled = false;
                btn_Set_Read.IsEnabled = false;
                btn_Limit_Read.IsEnabled = false;
                btn_Monitoring.IsEnabled = false;
                btn_RESET.IsEnabled = false;
                btn_Internal_ADC.IsEnabled = false;
                btn_ERROR_State.IsEnabled = false;
                btn_DelaySet.IsEnabled = false;
                btn_LIMIT_SET.IsEnabled = false;
                btn_AddressSet.IsEnabled = false;
            }
            else
            {
                btn_SET.IsEnabled = true;
                btn_poweronoff.IsEnabled = true;
                btn_vddonoff.IsEnabled = true;
                btn_elonoff.IsEnabled = true;
                btn_Set_Read.IsEnabled = true;
                btn_Limit_Read.IsEnabled = true;
                btn_Monitoring.IsEnabled = true;
                btn_RESET.IsEnabled = true;
                btn_Internal_ADC.IsEnabled = true;
                btn_ERROR_State.IsEnabled = true;
                btn_DelaySet.IsEnabled = true;
                btn_LIMIT_SET.IsEnabled = true;
                btn_AddressSet.IsEnabled = true;
            }
        }

        private void btnGMSOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GMSserialPort.PortName = cGMSBoxComPort.Text;
                GMSserialPort.BaudRate = Convert.ToInt32(cGMSBoxBaudRate.Text);
                GMSserialPort.DataBits = Convert.ToInt32(cGMSBoxDataBits.Text);
                GMSserialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cGMSBoxStopBits.Text);
                GMSserialPort.Parity = (Parity)Enum.Parse(typeof(Parity), cGMSBoxParityBits.Text);
                GMSserialPort.Open(); // Open port.
                GMSserialPort.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(GMSserialPort_DataRecieved);
                pGMSBar.Value = 100;
            }
            catch (Exception err)
            {
                System.Windows.MessageBox.Show(err.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnGMSClose_Click(object sender, RoutedEventArgs e)
        {
            if (GMSserialPort.IsOpen)
            {
                GMSserialPort.Close(); // Close port.

                pGMSBar.Value = 0;
            }
        }

        private void btnGMSSend_Click(object sender, RoutedEventArgs e)
        {
            if (GMSserialPort.IsOpen)
            {
                byte[] bytesToSend = new byte[6] { 0xAA, 0x55, 0x02, 0xFE, 0x01, 0x00 };
                //string str = "AA 55 02 FE 01 00";
                GMSserialPort.Write(bytesToSend, 0, bytesToSend.Length);
            }
        }

        private delegate void GMSUpdateUiTextDelegate(string text);

        private void GMSserialPort_DataRecieved(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            int bytes = GMSserialPort.BytesToRead;
            byte[] buffer = new byte[bytes];
            byte data;
            if (GMSserialPort.BytesToRead > 6)
            {
                GMSserialPort.Read(buffer, 0, bytes);
                //GMSrecievedData = BitConverter.ToString(buffer);
                data = buffer[4];
                GMSrecievedData = Convert.ToString(data);
                //GMSrecievedData = Convert.ToString(bytes);
                Dispatcher.Invoke(DispatcherPriority.Send, new GMSUpdateUiTextDelegate(GMSDataWrited), GMSrecievedData);
            }

        }

        private void GMSDataWrited(string text)
        {
            int data = Convert.ToInt32(text, 16);
            GMSdata.Content = "";
            GMSdata.Content = String.Format("{0} Ω", text);

            if (data > 10)
            {
                btnGMSOKNG.Background = Brushes.Red;
                tGMSBoxInData.AppendText(String.Format(" {0} --------------------------------- NG\n", text));
                tGMSBoxInData.Select(tGMSBoxInData.Text.Length, 0);
                tGMSBoxInData.ScrollToEnd();
            }
            else
            {
                btnGMSOKNG.Background = Brushes.Green;
                tGMSBoxInData.AppendText(String.Format(" {0} --------------------------------- OK\n", text));
                tGMSBoxInData.Select(tGMSBoxInData.Text.Length, 0);
                tGMSBoxInData.ScrollToEnd();
            }

            /*
            if (text.Contains("F6"))
            {
                try
                {


                    //tGMSBoxInData.AppendText(text.Substring(12, 2) + " VDD I: " + text.Substring(15, 2));
                    tGMSBoxInData.AppendText(text);
                    tGMSBoxInData.Select(tGMSBoxInData.Text.Length, 0);
                    tGMSBoxInData.ScrollToEnd();
                }
                catch (Exception e)
                {

                }
            }
            else
            {
                tGMSBoxInData.AppendText(System.DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss]") + text + "\n");
                tGMSBoxInData.Select(tGMSBoxInData.Text.Length, 0);
                tGMSBoxInData.ScrollToEnd();
            }*/

        }

        private void BtnCalData_Pick(object sender, RoutedEventArgs e)
        {
            var fileContent = string.Empty;
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                // 현재 위치 경로
                string currentDirectoryPath = Environment.CurrentDirectory.ToString();
                // Logs 디렉토리 경로(현재 경로에 Logs라는 디렉토리 경로 합치기)
                string DirPath = System.IO.Path.Combine(currentDirectoryPath, "EEPROM_DATA");

                openFileDialog.InitialDirectory = DirPath;
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    //Get the path of specified file
                    filePath = openFileDialog.FileName;
                    SET_EL_Data_Pick.Text = filePath.Split('\\')[filePath.Split('\\').Length - 1];
                    //Read the contents of the file into a stream
                    var fileStream = openFileDialog.OpenFile();

                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        // 읽은 전체 txt
                        fileContent = reader.ReadToEnd();

                        CompareCalData1 = fileContent.Split(new string[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
                        for (array1 = 0; array1 < CompareCalData1.Length; array1++)
                        {
                            MainWindow.CompareLog("CompareCalData1 [" + array1 + "] : " + CompareCalData1[array1]);
                            tBoxInData2.AppendText("CompareCalData1 [" + array1 + "] : " + CompareCalData1[array1]+"\n");
                            tBoxInData2.Select(tBoxInData.Text.Length, 0);
                            tBoxInData2.ScrollToEnd();
                        }
                    }
                }
            }
        }
    }
}
