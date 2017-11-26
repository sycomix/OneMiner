﻿using OneMiner.Core;
using OneMiner.Core.Interfaces;
using OneMiner.Model;
using OneMiner.Model.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace OneMiner.Coins.EthHash
{
    /// <summary>
    /// this class does not represent a miner program. coz this contains specif info like batfilepath etc
    /// this represents a miner program inside a configured miner. there could be many miners of the same type. eg ethereum, ethereum_sia
    /// for the real representation fo a miner program, look at JsonData.MinerProgram
    /// </summary>
    class ClaymoreMiner : IMinerProgram
    {

        private const string MINERURL = "https://github.com/nanopool/Claymore-Dual-Miner/releases/download/v10.0/Claymore.s.Dual.Ethereum.Decred_Siacoin_Lbry_Pascal.AMD.NVIDIA.GPU.Miner.v10.0.zip";
        private const string EXENAME = "EthDcrMiner64.exe";
        private const string PROCESSNAME = "EthDcrMiner64";
        private const string STATS_LINK = "http://127.0.0.1:3000/";

        public string Script { get; set; }
        public IOutputReader Reader { get; set; }

        public string MinerFolder { get; set; }
        public string MinerEXE { get; set; }
        public string BATFILE { get; set; }
        public bool BATCopied { get; set; }

        public bool AutomaticScriptGeneration { get; set; }


        public MinerProgramState MinerState { get; set; }

        public IMiner Miner { get; set; }
        public ICoin MainCoin { get; set; }
        public ICoin DualCoin { get; set; }

        public ICoinConfigurer MainCoinConfigurer { get; set; }
        public ICoinConfigurer DualCoinConfigurer { get; set; }
        public bool DualMining { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }//claymore ccminer etc
        MinerDownloader m_downloader = null;
        private Process m_Process = null;
        private object m_accesssynch = new object();
        IOutputReader m_OutputReader = new ClayMoreReader(STATS_LINK);



        public ClaymoreMiner(ICoin mainCoin, bool dualMining, ICoin dualCoin, string minerName, IMiner miner)
        {

            MinerState = MinerProgramState.Stopped;

            MainCoin = mainCoin;
            MainCoinConfigurer = mainCoin.SettingsScreen;
            DualCoin = dualCoin;
            if (DualCoin != null)
                DualCoinConfigurer = DualCoin.SettingsScreen;
            DualMining = dualMining;
            Name = minerName;
            Miner = miner;
            AutomaticScriptGeneration = true;
            Type = "Claymore";
            m_downloader = new MinerDownloader(MINERURL, EXENAME);
            GenerateScript();

        }



        public bool ReadyForMining()
        {
            return MiningScriptsPresent() && ProgramPresent() && BATCopied;
        }
        public bool MiningScriptsPresent()
        {
            if (BATFILE == null || BATFILE == "")
                return false;
            FileInfo script = new FileInfo(BATFILE);
            if (script.Exists)
                return true;
            return ProgramPresent();
        }
        public bool ProgramPresent()
        {
            if (MinerEXE == null || MinerEXE == "")
                return false;
            FileInfo miner = new FileInfo(MinerEXE);
            if (miner.Exists)
                return true;
            return false;
        }
        public void SaveProgramToDB()
        {
            if (ProgramPresent())
            {
                Config model = Factory.Instance.Model;
                model.AddMinerProgram(this);

            }
        }
        public void SaveScriptToDB()
        {
            if (MiningScriptsPresent())
            {
                Config model = Factory.Instance.Model;
                model.AddMinerScript(this, Miner);

            }
        }

        public string FormBatFileName(string folder)
        {
            return folder + @"\" + Miner.Name + ".bat";
        }
        public void DownloadProgram()
        {
            try
            {
                if (!ProgramPresent())
                {
                    MinerState = MinerProgramState.Downloading;
                    Miner.SetRunningState(this, MinerState);

                    MinerFolder = m_downloader.DownloadFile();
                    MinerEXE = MinerFolder + @"\" + EXENAME;
                    SaveProgramToDB();

                }
                string actualBatfileName = FormBatFileName(MinerFolder);

                if (AutomaticScriptGeneration == false)
                {
                    //this might be becoz user has edited the bat file
                    FileInfo file = new FileInfo(BATFILE);
                    if (file.Exists)
                    {
                        file.CopyTo(actualBatfileName, true);
                    }
                }
                BATFILE = actualBatfileName;
                ConfigureMiner();
                BATCopied = true;
                SaveScriptToDB();
                MinerState = MinerProgramState.Stopped;
            }
            catch (Exception e)
            {
                Logger.Instance.LogError(e.Message);
            }
        }

        public void StartMining()
        {
            //lock ensures that neither can someone kill a miner while it is being started, nor can 2 people start it at same time
            lock (m_accesssynch)
            {
                try
                {
                    FileInfo file = new FileInfo(BATFILE);
                    if (Factory.Instance.CoreObject.MiningCommand!=MinerProgramCommand.Run)
                    {
                        throw new Exception("Mining command is not 'Run'");
                    }
                    if (m_Process != null)
                    {
                        throw new Exception("Process object is not null while starting");
                    }
                    if (file.Exists)
                    {
                        MinerState = MinerProgramState.Running;
                        ProcessStartInfo info = new ProcessStartInfo();
                        info.UseShellExecute = false;
                        //Todo: Enable this when we have feature to configure the settings
                        //info.CreateNoWindow = ! Factory.Instance.Model.Data.Option.ShowMinerWindows;
                        info.FileName = BATFILE;
                        info.WindowStyle = ProcessWindowStyle.Hidden;
                        info.WorkingDirectory = file.DirectoryName + "\\";

                        m_Process = new Process();
                        m_Process.StartInfo = info;
                        bool success = m_Process.Start();
                        if (success)
                        {
                            MinerState = MinerProgramState.Running;
                            Miner.SetRunningState(this, MinerProgramState.Running);
                            Alarm.RegisterForTimer(m_OutputReader.AlarmRaised);
                        }

                    }

                }
                catch (Exception e)
                {
                    Logger.Instance.LogError(e.ToString());
                }
                finally
                {
                    //MinerState = MinerProgramState.Stopped;
                }
            }
        }
        public void SetRunningState(MinerProgramState state)
        {
            lock (m_accesssynch)
            {
                try
                {
                    MinerState = state;
                    Miner.SetRunningState(this, state);
                }
                catch (Exception e)
                {
                }
            }
        }
        public void KillMiner()
        {
            lock (m_accesssynch)
            {
                try
                {
                    if (m_Process != null)
                    {
                        try
                        {
                            //this actually dos4nt work as we get handle to command prompt used by the miner as its a batch file
                            m_Process.Kill();
                        }
                        catch (Exception e)
                        {
                            Logger.Instance.LogError(e.ToString());
                        }
                    }
                    Process[] allprocess = Process.GetProcessesByName(PROCESSNAME);//this does the job
                    if (allprocess != null && allprocess.Length > 0)
                    {
                        foreach (Process item in allprocess)
                        {
                            item.Kill();
                        }
                    }
                    m_Process = null;
                    MinerState = MinerProgramState.Stopped;
                    Miner.SetRunningState(this, MinerState);

                }
                catch (Exception e)
                {
                    Logger.Instance.LogError(e.ToString());
                }
            }
        }

        public bool Running()
        {
            bool running = false;
            try
            {
                running = !m_Process.HasExited;
            }
            catch (Exception e)
            {
                running = false;
            }
            return running;
        }



        public string GenerateScript()
        {
            try
            {
                //generate script and write to folder
                string command = EXENAME + " -epool " + MainCoinConfigurer.Pool;
                command += " -ewal " + MainCoinConfigurer.Wallet;
                command += " -epsw x ";
                if (DualCoin != null)
                {
                    command += " -dpool " + DualCoinConfigurer.Pool;
                    command += " -dwal " + MainCoinConfigurer.Wallet;
                    command += " -ftime 10 ";

                }

                Script = SCRIPT1 + command;
                AutomaticScriptGeneration = true;
                SaveScriptToDB();
                return Script;
            }
            catch (Exception e)
            {
                return "";
            }
        }
        public void ModifyScript(string script)
        {
            Script = script;
            string tempBatFile = "";
            string tempBatFileFolder = "";
            if (MinerFolder != null && MinerFolder != "")
                tempBatFileFolder = MinerFolder;
            else
                tempBatFileFolder = m_downloader.GetTempBatFile(Miner.Id, Type, Miner.Name);
            tempBatFile = FormBatFileName(tempBatFileFolder);

            if (tempBatFile != "")
            {
                BATFILE = tempBatFile;
                SaveToBAtFile();
                AutomaticScriptGeneration = false;
                SaveScriptToDB();
            }
        }
        private void ConfigureMiner()
        {
            try
            {
                if (AutomaticScriptGeneration == false)
                    return;

                GenerateScript();
                SaveToBAtFile();

            }
            catch (Exception e)
            {
            }
        }
        private void SaveToBAtFile()
        {
            try
            {
                FileStream stream = File.Open(BATFILE, FileMode.Create);
                StreamWriter sw = new StreamWriter(stream);
                sw.Write(Script);
                sw.Flush();
                sw.Close();
                //generate script and write to folder

            }
            catch (Exception e)
            {
            }
        }
        public void LoadScript()
        {
            try
            {
                if (AutomaticScriptGeneration)
                {
                    GenerateScript();
                }
                else
                {
                    FileStream stream = File.Open(BATFILE, FileMode.Open);
                    StreamReader sr = new StreamReader(stream);
                    Script = sr.ReadToEnd();
                    sr.Close();
                }
            }
            catch (Exception e)
            {
            }
        }



        private const string SCRIPT1 =
@"setx GPU_FORCE_64BIT_PTR 0
setx GPU_MAX_HEAP_SIZE 100
setx GPU_USE_SYNC_OBJECTS 1
setx GPU_MAX_ALLOC_PERCENT 100
setx GPU_SINGLE_ALLOC_PERCENT 100
";



        /// <summary>
        /// reads data for claymore miner
        /// </summary>
        class ClayMoreReader:IOutputReader
        {
            private object s_accesssynch = new object();
            public string StatsLink { get; set; }
            public string m_Lastlog = "";
            public string LastLog
            {
                get
                {
                    lock (s_accesssynch)
                    {
                        try
                        {
                            return m_Lastlog;
                        }
                        catch (Exception e)
                        {
                            Logger.Instance.LogError(e.ToString());
                        }
                        return "";
                    }
                }            
                set
                {
                    lock (s_accesssynch)
                    {
                        try
                        {
                            m_Lastlog=value;
                        }
                        catch (Exception e)
                        {
                            Logger.Instance.LogError(e.ToString());
                        }
                        m_Lastlog = "";
                    }
                }
            }

            public ClayMoreReader(string link)
            {
                StatsLink = link;
            }
            public void Read()
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(StatsLink);
                request.Method = "GET";
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 7.1; Trident/5.0)";
                request.Accept = "/";
                request.UseDefaultCredentials = true;
                request.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                //doc.Save(request.GetRequestStream());
                HttpWebResponse resp = request.GetResponse() as HttpWebResponse;
                Stream stream = resp.GetResponseStream();
                StreamReader sr = new StreamReader(stream);
                LastLog = sr.ReadToEnd();
            }
            public void Parse()
            {
               
            }
            public void AlarmRaised()
            {
                Read();
                Parse();
            }
        }
    }

}
