#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class WPBreakOfStructure : Strategy
	{
		private double dailyMax = 0;
		private double dailyMin = 0;
		private int barNumber = 0;
		
		#region BarIndexing
		private int barCount = 0;
		
		private TSSuperTrend tstrend;
		
		#region BarIndexing
		public Stack<IPoints> DailyHighPoints = new Stack<IPoints>();
		public Stack<IPoints> DailyLowPoints = new Stack<IPoints>();
		
		public Stack<IPoints> InternalHighPoints = new Stack<IPoints>();
		public Stack<IPoints> InternalLowPoints = new Stack<IPoints>();
		#endregion
		
		#endregion
		
		#region Structs
		public struct IPoints {
			public IPoints (int barCounterIndex, int barNumberIndex, double price)
		    {
		        BarCounterIndex 	= barCounterIndex;
				BarNumberIndex 		= barNumberIndex;
		    	Price				= price;
			}
		
			public int BarCounterIndex { get; }
			public int BarNumberIndex { get; }
			public double Price	{ get; }
			
		    public int BarsAgo(int currentBar) => Math.Abs(currentBar - BarCounterIndex);
		}
		#endregion
		
		private double doubleTickSize = 0;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "WPBreakOfStructure";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
				
				AddPlot(Brushes.White, "EMA9");
				AddPlot(Brushes.Gold, "SMA20");
				AddPlot(Brushes.DeepSkyBlue, "SMA50");
			}
			else if (State == State.Configure)
			{
			}
			else if (State == State.DataLoaded) {
				doubleTickSize = TickSize * 2;
				tstrend = TSSuperTrend(SuperTrendMode.ATR, 14, 2.618, MovingAverageType.HMA, 14, true, true, false);
			}
		}
		
		private IPoints GetLastDailyHighPoint() {
			return DailyHighPoints.ToArray()[0];
		}
		
		private IPoints GetLastDailyLowPoint() {
			return DailyLowPoints.ToArray()[0];
		}
		
		private IPoints GetLastInternalHighPoint() {
			return InternalHighPoints.ToArray()[0];
		}
		
		private IPoints GetLastInternalLowPoint() {
			return InternalLowPoints.ToArray()[0];
		}
		
		private void UpdateHighAndLows(IPoints lastDailyHigh, IPoints lastDailyLow) {
			if (High[0] > lastDailyHigh.Price) {
				DailyHighPoints.Push(new IPoints(barCount, barNumber, High[0]));
			}
			else if (Low[0] < lastDailyLow.Price) {
				DailyLowPoints.Push(new IPoints(barCount, barNumber, Low[0]));
			}
		}

		protected override void OnBarUpdate()
		{
			//Add your custom strategy logic here.
			if (BarsInProgress != 0)
				return;
			
			Values[0][0] = EMA(Close, 9)[0];
			Values[1][0] = SMA(Close, 20)[0];
			Values[2][0] = SMA(Close, 50)[0];
			
			barNumber = CurrentBar;
			
			 if (order != null && order.IsBacktestOrder && State == State.Realtime)
      			order = GetRealtimeOrder(order);
			
			if (CurrentBars[0] < 1)
				return;
			
			if (Bars.IsFirstBarOfSession) {
				barCount = 0;
				Draw.Diamond(this, "FirstBar" + barNumber, true, 0, High[0] + (TickSize * 50), Brushes.AliceBlue);
			}
			
			// Seta pela primeira vez as Maximas e Minimas do dia
			// Desenha um diamante no primeiro candle
			if (barCount == 2 && IsFirstTickOfBar) {
				IPoints dhp = new IPoints(barCount, barNumber, High[2]);
				DailyHighPoints.Push(dhp);
				double yhigh = (GetLastDailyHighPoint()).Price;
				Draw.Text(this, "FirstHigh" + CurrentBar, "[ H ]", 2, yhigh + (TickSize * 20));
				Draw.Line(this, "HighPoint" + CurrentBar, true, 3,  yhigh, 2, yhigh, Brushes.Aquamarine, DashStyleHelper.Solid, 1, true);
			
				IPoints dlp = new IPoints(barCount, barNumber, Low[2]);
				DailyLowPoints.Push(dlp);
				double ylow = (GetLastDailyLowPoint()).Price;
				Draw.Text(this, "FirstLow" + CurrentBar, "[ L ]", 2, ylow - (TickSize * 20));
				Draw.Line(this, "LowerPoint" + CurrentBar, true, 3, ylow, 2, ylow, Brushes.Aquamarine, DashStyleHelper.Solid, 1, true);
			}
			
			if (barCount >= 2 && IsFirstTickOfBar) {
				if (DailyHighPoints.Count == 0 || DailyLowPoints.Count == 0) return;
				
				IPoints lastDailyHigh = GetLastDailyHighPoint();
				IPoints lastDailyLow = GetLastDailyLowPoint();
				
				// Broke Structure
				if (Close[0] > lastDailyHigh.Price) {			
					int barsAgo = lastDailyHigh.BarsAgo(barCount);
					Draw.Line(this, "HighPoint" + CurrentBar, true, barsAgo, lastDailyHigh.Price, 0, lastDailyHigh.Price, Brushes.Aquamarine, DashStyleHelper.Solid, 1, true);
					DailyHighPoints.Push(new IPoints(barCount, barNumber, High[0]));
					
					int lastMinBarAgo = LowestBar(Low, barsAgo);
					double lastMinBarAgoPrice = Low[lastMinBarAgo];
					if (lastMinBarAgo > 2) 
						InternalLowPoints.Push(new IPoints(lastMinBarAgo, 0, lastMinBarAgoPrice));
						Draw.Line(this, "InternalLowPoint" + CurrentBar, true, lastMinBarAgo, lastMinBarAgoPrice, 0, lastMinBarAgoPrice, Brushes.DarkRed, DashStyleHelper.Dot, 1, true);
					
					// Update Stops
					TrailingStop(lastMinBarAgoPrice);
					
					if (tstrend.UpTrend.Count > 0 && Close[0] > tstrend.UpTrend[0]) { // && State != State.Historical) {
						if (Position.MarketPosition == MarketPosition.Flat) {
							SetProfitTarget(CalculationMode.Ticks, Math.Abs(lastMinBarAgoPrice - lastDailyHigh.Price) * 4);
							SetStopLoss(CalculationMode.Price, lastMinBarAgoPrice - (TickSize * 2));
							order = EnterLong(DefaultQuantity, "Long");
						}
					}
					
					UpdateHighAndLows(lastDailyHigh, lastDailyLow);
				} 
				
				// Broke Structure
				else if (Close[0] < lastDailyLow.Price) {
					int barsAgo = lastDailyLow.BarsAgo(barCount);
					Draw.Line(this, "LowPoint" + CurrentBar, true, barsAgo, lastDailyLow.Price, 0, lastDailyLow.Price, Brushes.Chocolate, DashStyleHelper.Solid, 1, true);
					DailyLowPoints.Push(new IPoints(barCount, barNumber, Low[0]));
					
					int lastMaxBarAgo = HighestBar(High, barsAgo);
					double lastMaxBarAgoPrice = High[lastMaxBarAgo];
					if (lastMaxBarAgo > 2) 
						InternalLowPoints.Push(new IPoints(lastMaxBarAgo, 0, lastMaxBarAgoPrice));
						Draw.Line(this, "InternalHighPoint" + CurrentBar, true, lastMaxBarAgo, lastMaxBarAgoPrice, 0, lastMaxBarAgoPrice, Brushes.DarkGreen, DashStyleHelper.Dot, 1, true);

					// Update Stops
					TrailingStop(lastMaxBarAgoPrice);
					
					if (tstrend.DownTrend.Count > 0 && Close[0] < tstrend.DownTrend[0] ) {// && State != State.Historical) {
						if (Position.MarketPosition == MarketPosition.Flat) {
							SetProfitTarget(CalculationMode.Ticks, Math.Abs(lastMaxBarAgoPrice - lastDailyLow.Price) * 4);
							SetStopLoss(CalculationMode.Price, lastMaxBarAgoPrice + (TickSize * 2));
							order = EnterShort(DefaultQuantity, "Short");
						}
					}
					
					UpdateHighAndLows(lastDailyHigh, lastDailyLow);
				}
			}
			barCount++;
		}
		
		#region Properties

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> EMA9
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> SMA20
		{
			get { return Values[1]; }
		}
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> SMA50
		{
			get { return Values[2]; }
		}
		#endregion
	}
}
