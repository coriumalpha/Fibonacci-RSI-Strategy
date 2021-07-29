#region usings
using System;
using System.Collections.Generic;
using System.Security;

using Plugins;
using Plugins.Indicator;
using Plugins.Strategy;

using Tengoku.Kumo.Calc.Indicators;
using Tengoku.Kumo.Charting.Chart;
using Tengoku.Kumo.Charting.Frame;
using Tengoku.Kumo.Charting.Scale;

using Tengoku.VisualChart.Plugins.Attributes;
using Tengoku.VisualChart.Plugins.Types;
using VisualChart.Development.Runtime.DataSeries;
using VisualChart.Development.Runtime.Plugins;
#endregion

namespace FiboRS1
{
    /// <summary>
    /// Fibonacci + RSI Strategy
    /// Works best on 1 minute chart
    /// Unoptimized default parameters
    /// </summary>
    [Strategy(Name = "FiboRS1", Description = "Fibonacci + RSI Strategy")]
    public class FiboRS1 : StrategyPlugin
    {
        //TODO: Include Parabolic SAR parameters
        [Parameter(Name = "Stop Loss (%)", DefaultValue = 2, MinValue = 0, MaxValue = 100, Step = 0.01)]
        private double StopLossPercent;

        [Parameter(Name = "RSI Length", DefaultValue = 14, MinValue = 0, MaxValue = 100, Step = 1)]
        private long RsiLength;

        [Parameter(Name = "RSI Over Sold", DefaultValue = 30, MinValue = 0, MaxValue = 100, Step = 1)]
        private long RsiOverSold;

        [Parameter(Name = "RSI Over Bought", DefaultValue = 70, MinValue = 0, MaxValue = 100, Step = 1)]
        private long RsiOverBought;

        [Parameter(Name = "Fibo Length", DefaultValue = 200, MinValue = 1, Step = 1)]
        private long FiboLength;

        [Parameter(Name = "Fibo Multiplier", DefaultValue = 3, MinValue = 0.001, MaxValue = 50, Step = 1)]
        private double FiboMultiplier;

        /// <summary>
        /// Fibo levels by index (1 = 382, 2 = 500, 3 = 618, 4 = 764)
        /// </summary>
        [Parameter(Name = "Fibo Level", DefaultValue = 4, MinValue = 1, MaxValue = 4, Step = 1)]
        private long FiboLevelIndex;


        //This class depends on https://github.com/coriumalpha/Fibonacci-Indicator
        itsasontsi_Fu764 Fu764;
        RSI Rsi;
        //ParabolicSAR ParabolicSar;

        //For strategy optimization purposes
        Dictionary<int, int> FiboLevels = new Dictionary<int, int>()
        {
            {1, 382},
            {2, 500},
            {3, 618},
            {4, 764},
        };

        long FiboLevel;

        /// <summary>
        /// This method is used to configure the strategy and is called once before any strategy method is called.
        /// </summary>
        public override void OnInitCalculate()
        {
            FiboLevel = GetFiboLevelByIndex((int) FiboLevelIndex);

            Fu764 = new itsasontsi_Fu764(this.Data, FiboLength, FiboLevel, FiboMultiplier);
            Rsi = new RSI(this.Data, RsiLength, RsiOverBought, RsiOverSold);
            //ParabolicSar = new ParabolicSAR(this.Data);
        }

        /// <summary>
        /// Called on each bar update event.
        /// </summary>
        /// <param name="bar">Bar index.</param>
        public override void OnCalculateBar(int bar)
        {
            //Set target by adquiring the fibo-level target bands (lines 2 and 4)
            double targetUp = Fu764.Value(0, 4);
            double targetDown = Fu764.Value(0, 2);

            //Calculate RSI crosses
            int oversoldCross = this.CrossType(Rsi.Value(1), RsiOverSold, Rsi.Value(), RsiOverSold);
            int overbougthCross = this.CrossType(Rsi.Value(1), RsiOverBought, Rsi.Value(), RsiOverBought);

            //Check trade conditions and operate accordingly
            ProccessOrders(targetUp, targetDown, oversoldCross, overbougthCross);
        }

        #region Private Methods

        private void ProccessOrders(double targetUp, double targetDown, int oversoldCross, int overbougthCross)
        {
            ProccessEntryOrders(targetUp, targetDown, oversoldCross, overbougthCross);
            ProccessExitOrders(targetUp, targetDown);
        }

        private void ProccessEntryOrders(double targetUp, double targetDown, int oversoldCross, int overbougthCross)
        {
            //Enter long
            if ((this.Low() < Fu764.Value(0, 5)) && oversoldCross == -1 && this.High() < targetUp)
            {
                this.Buy(TradeType.AtMarket, 1, 0);
                this.StopLoss(-1, (double)StopLossPercent, GapType.Percentage, PositionSide.Buy);
            }

            //Enter short
            if ((this.High() < Fu764.Value(0, 1)) && overbougthCross == 1 && this.Low() > targetDown)
            {
                this.Sell(TradeType.AtMarket, 1, 0);
                this.StopLoss(-1, (double)StopLossPercent, GapType.Percentage, PositionSide.Sell);
            }
        }

        private void ProccessExitOrders(double targetUp, double targetDown)
        {
            long marketPosition = this.GetMarketPosition();

            if (marketPosition > 0)
            {
                //Exit long
                if (this.High() > targetUp)
                {
                    this.ExitLong(TradeType.AtMarket, -1, 0);
                }
            }
            else if (marketPosition < 0)
            {
                //Exit short
                if (this.Low() > targetDown)
                {
                    this.ExitShort(TradeType.AtMarket, -1, 0);
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Study for the average crossover.
        /// </summary>
        /// <param name="crossedPrev">Crossed average value in previous bar.</param>
        /// <param name="crossPrev">Cross average value in previous bar.</param>
        /// <param name="crossed">Crossed average value in current bar.</param>
        /// <param name="cross">Cross average value in current bar.</param>
        /// <returns>
        /// Function return 1 when an up cross happens.
        /// Function return -1 when a down cross happens.
        /// Function return 0 when there is no crossover at all.
        /// </returns>
        private int CrossType(double crossedPrev, double crossPrev, double crossed, double cross)
        {
            int crossVal = 0;
            if (crossedPrev != 2147483647 && crossedPrev != 2147483647)
            {
                if (cross > crossed && crossPrev <= crossedPrev) crossVal = 1;
                if (cross < crossed && crossPrev >= crossedPrev) crossVal = -1;
            }

            return crossVal;
        }

        private int GetFiboLevelByIndex(int fiboIndex)
        {
            return FiboLevels[fiboIndex];
        }

        #endregion

        #region Visual Chart Code

        /// <summary>
        /// Performs calculus between startBar and endBar.
        /// </summary>
        /// <param name="startBar">Initial calculus bar.</param>
        /// <param name="endBar">End calculus bar.</param>
        public override void OnCalculateRange(int startBar, int endBar)
        {
            int i = this.StartBar;
            if (startBar > i)
                i = startBar;

            while (!this.ShouldTerminate && i <= endBar)
            {
                this.CurrentBar = i;
                this.CalculateAggregators();
                this.OnCalculateBar(i);
                i++;
            }
        }

        /// <summary>
        /// Sets calculus parameters.
        /// </summary>
        /// <param name="paramList">Parameters list.</param>
        public override void OnSetParameters(List<object> paramList)
        {
            StopLossPercent = Convert.ToDouble(paramList[0]);
            RsiLength = Convert.ToInt32(paramList[1]);
            RsiOverSold = Convert.ToInt32(paramList[2]);
            RsiOverBought = Convert.ToInt32(paramList[3]);
            FiboLength = Convert.ToInt32(paramList[4]);
            FiboMultiplier = Convert.ToDouble(paramList[5]);
            FiboLevelIndex = Convert.ToInt32(paramList[6]);
        }

        /// <summary>
        /// This function is used to create the data series corresponding to any indicator and to obtain an identifier of this series. To do so, we need to declare first a variable DataIdentifier type. Once the variable is defined we will always assign to it the the value of the function GetIndicatorIdentifier in order to create the indicator data series and to obtain an identifier of the same indicator. The identifier of the indicator must be obtained from the procedure <see cref="OnInitCalculate"/>.
        /// Later on, in order to obtain the value of an indicator we must use the functionGetIndicatorValue and indicate in the parameter Data the variable on which we have saved the value of the corresponding indicator. The identifier obtained by this function can be use don any .NET function on which a Data is required (Data series on which the different functions are calculated).
        /// </summary>
        /// <param name="indicator">Indicator Id.</param>
        /// <param name="parentDataIdentifier">Identifier of the series on which the indicator is calculated. If we set this parameter as data, we will be calculating the indicator on the data or series on which the strategy is being calculated. If we are willing to obtain the identifier of an indicator being calculated on another indicator we shall indicate within this parameter the identifier of the indicator we are willing to use as calculation.</param>
        /// <param name="optionalParameters">Indicator parameters (can be null).</param>
        /// <returns>Indicator source identifier.</returns>        
        public DataIdentifier GetIndicatorIdentifier(Indicators indicator, DataIdentifier parentDataIdentifier, params object[] optionalParameters)
        {
            return base.GetIndicatorIdentifier((long)indicator, parentDataIdentifier, optionalParameters);
        }

        /// <summary>
        /// This function is used to create the data series corresponding to any indicator and to obtain an identifier of this series. To do so, we need to declare first a variable DataIdentifier type. Once the variable is defined we will always assign to it the the value of the function GetIndicatorIdentifier in order to create the indicator data series and to obtain an identifier of the same indicator. The identifier of the indicator must be obtained from the procedure <see cref="OnInitCalculate"/>.
        /// Later on, in order to obtain the value of an indicator we must use the functionGetIndicatorValue and indicate in the parameter Data the variable on which we have saved the value of the corresponding indicator. The identifier obtained by this function can be use don any .NET function on which a Data is required (Data series on which the different functions are calculated).
        /// </summary>
        /// <param name="indicator">Indicator Id.</param>
        /// <param name="parentDataIdentifier">Identifier of the series on which the indicator is calculated. If we set this parameter as data, we will be calculating the indicator on the data or series on which the strategy is being calculated. If we are willing to obtain the identifier of an indicator being calculated on another indicator we shall indicate within this parameter the identifier of the indicator we are willing to use as calculation.</param>
        /// <param name="optionalParameters">Indicator parameters (can be null).</param>
        /// <returns>Indicator source identifier.</returns> 
        public DataIdentifier GII(Indicators indicator, DataIdentifier parentDataIdentifier, params object[] optionalParameters)
        {
            return base.GII((long)indicator, parentDataIdentifier, optionalParameters);
        }

        /// <summary>
        /// This function enables to obtain internally, the information of a certain system. This way, we can extract the information from this system without having to calculate it once and once again.
        /// </summary>
        /// <param name="strategy">Strategy id.</param>
        /// <param name="parentDataIdentifier">Identifier of the series on which the system is calculated. If we set this parameter as data, we will be calculating the indicator on the data or series on which the strategy is being calculated. If we are willing to obtain the identifier of an indicator being calculated on another indicator we shall indicate within this parameter the identifier of the indicator we are willing to use as calculation.</param>
        /// <param name="optionalParameters">System parameters (can be null).</param>
        /// <returns>system source identifier.</returns>
        public DataIdentifier GetSystemIdentifier(Strategies strategy, DataIdentifier parentDataIdentifier, params object[] optionalParameters)
        {
            return base.GetSystemIdentifier((long)strategy, parentDataIdentifier, optionalParameters);
        }

        /// <summary>
        /// This function enables to obtain internally, the information of a certain system. This way, we can extract the information from this system without having to calculate it once and once again.
        /// </summary>
        /// <param name="strategy">Strategy id.</param>
        /// <param name="parentDataIdentifier">Identifier of the series on which the system is calculated. If we set this parameter as data, we will be calculating the indicator on the data or series on which the strategy is being calculated. If we are willing to obtain the identifier of an indicator being calculated on another indicator we shall indicate within this parameter the identifier of the indicator we are willing to use as calculation.</param>
        /// <param name="optionalParameters">System parameters (can be null).</param>
        /// <returns>system source identifier.</returns>
        public DataIdentifier GSYSI(Strategies strategy, DataIdentifier parentDataIdentifier, params object[] optionalParameters)
        {
            return base.GSYSI((long)strategy, parentDataIdentifier, optionalParameters);
        }

        #endregion
    }
}
