﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneMiner.Core.Interfaces
{
    interface IHashAlgorithm
    {
        /// <summary>
        /// Name of the algo
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Coins supported by this algo. eg: Ethhash would support Ethereum , Ubiq etc
        /// </summary>
        List<ICoin> SupportedCoins { get; }

        /// <summary>
        /// Some algo can profitably dual mine eg ethhash but not equihash
        /// </summary>
        Boolean SupportsDualMining { get; }

        /// <summary>
        /// List of coin s supportde by this algo
        /// </summary>
        List<ICoin> SupportedDualCoins { get; }

    }
}