﻿using OneMiner.Coins.EthHash;
using OneMiner.Core;
using OneMiner.Core.Interfaces;
using OneMiner.Model.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneMiner.EthHash
{
    public class EthereumData : MinerData,IMiner
    {
        public List<IMinerProgram> MinerPrograms { get; set; }
        public ICoin MainCoin { get; set; }
        public ICoin DualCoin { get; set; }

        public bool  DualMining { get; set; }

        public string Name { get; set; }
        public string Logo { get; set; }//not sure if needed

        public EthereumData (ICoin mainCoin,bool dualMining, ICoin dualCoin, string minerName)
        {
            MainCoin = mainCoin;
            DualCoin = dualCoin;
            DualMining = dualMining;
            Name = minerName;
            MinerPrograms = new List<IMinerProgram>();
            SetupMiner();
        }

        public void SetupMiner()
        {
            MinerPrograms.Add(new ClaymoreMiner(MainCoin,DualMining, DualCoin, Name));

        }
        public void StartMining()
        {

            foreach (IMinerProgram item in MinerPrograms)
            {
                //push miners into mining queue wher they wud be picked up by threads and executed
                Factory.Instance.CoreObject.MiningQueue.Enqueue(item);
            }
        }
        public void StopMining()
        {
            foreach (IMinerProgram item in MinerPrograms)
            {
                //push miners into mining queue wher they wud be picked up by threads and executed
                item.KillMiner();
            }
        }


        
    }
}
