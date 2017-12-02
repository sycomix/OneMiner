﻿using OneMiner.Core.Interfaces;
using OneMiner.View.v1;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace OneMiner.Coins.EthHash
{
    class Expanse : ICoin
    {
        public IHashAlgorithm Algorithm { get; set; }

        ICoinConfigurer Configurer;


        public Expanse(IHashAlgorithm algo)
        {
            Algorithm = algo;

        }
        public  string Name
        {
            get { return "Expanse"; }
        }

        public Bitmap Logo
        {
            get { return OneMiner.Properties.Resources.expanse; }
            
        }

        public ICoinConfigurer SettingsScreen
        {
            get
            {
                if (Configurer == null)
                {
                    Configurer = new ConfigureMiner();
                    Configurer.AssignCoin(this);
                }
                return Configurer;
            }
        }
    }
}