﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using StringFormatEx.Plugins.Core;



namespace StringFormatEx.Plugins
{
	public class TimestringPlugin : IStringFormatterPlugin
    {
   	    private static readonly FormattingOptions _defaultFormattingOptions = CreateDefaultFormattingOptions();
   	    private readonly FormattingOptions _formattingOptions;


        public TimestringPlugin() 
            : this(CreateDefaultFormattingOptions())
        {}

        public TimestringPlugin(FormattingOptions formattingOptions)
        {
            _formattingOptions = formattingOptions;
        }



        #region AccuracyOptions

        public enum AccuracyOptions
		{
			Milliseconds,
			Seconds,
			Minutes,
			Hours,
			Days,
			Weeks
		}

        #endregion


        #region TruncationOptions

        public enum TruncationOptions
		{
			/// <summary>
			/// Automatically removes any values that are zero.
			/// Example: "10.23:00:59.000" = "10 days 23 hours 59 minutes"
			/// </summary>
			Auto,
			/// <summary>
			/// Only keeps the highest non-zero value.
			/// Example: "10.23:00:59.000" = "10 days"
			/// </summary>
			Shortest,
			/// <summary>
			/// Starts with the highest non-zero value and displays all lesser values.
			/// Example: "0.0:10:59.000" = "10 minutes 59 seconds 0 milliseconds"
			/// </summary>
			Fill,
			/// <summary>
			/// Displays all values within range.
			/// Example: "0.0:10:59.000" = "0 days 0 hours 10 minutes 59 seconds 0 milliseconds"
			/// </summary>
			Full
		}

        #endregion


        #region FormattingOptions 

        public class FormattingOptions
        {
            public AccuracyOptions SmallestUnitToDisplay { get; set; }
            public AccuracyOptions LargestUnitToDisplay { get; set; }
            public TruncationOptions TruncationOption { get; set; }
            public bool Abbreviate { get; set; }
            public bool IfZeroIncludeLessThan { get; set; }
        }

        #endregion


        #region DefaultFormattingOptions

        public static FormattingOptions CreateDefaultFormattingOptions()
        {
            return new FormattingOptions() {
                                               SmallestUnitToDisplay = AccuracyOptions.Seconds,
                                               LargestUnitToDisplay = AccuracyOptions.Days,
                                               TruncationOption = TruncationOptions.Auto,
                                               Abbreviate = false,
                                               IfZeroIncludeLessThan = true
                                           };
        }

        #endregion


        public IEnumerable<EventHandler<ExtendSourceEventArgs>> GetSourceExtensions()
        {
            return new EventHandler<ExtendSourceEventArgs>[] { };
        }

        public IEnumerable<EventHandler<ExtendFormatEventArgs>> GetFormatExtensions()
        {
            return new EventHandler<ExtendFormatEventArgs>[] 
                { this.DoTimeStringFormat };
        }


		private void DoTimeStringFormat(object sender, ExtendFormatEventArgs e)
		{
		    CustomFormatInfo info = e.FormatInfo;

			if (info.CurrentIsTimeSpan) {
				info.Write(TimestringPlugin.ToTimeString((TimeSpan)info.Current, info.Format, _formattingOptions));
			} else if (info.CurrentIsDate && info.FormatStartsWith("timestring")) {
				info.Write(TimestringPlugin.ToTimeString(DateTime.Now.Subtract((DateTime)info.Current), info.Format.Substring(10), _formattingOptions));
			}
		}



        public static string ToTimeString(TimeSpan FromTime)
        {
            return ToTimeString(FromTime, _defaultFormattingOptions);
        }

        public static string ToTimeString(TimeSpan FromTime, string formatOptions)
        {
            return ToTimeString(FromTime, formatOptions, _defaultFormattingOptions);
        }
        

        /// <summary>
		/// Turns a TimeSpan into a human-readable text.  
		/// For example: "31.23:59:00.555" = "31 days 23 hours 59 minutes 0 seconds 555 milliseconds"
		/// </summary>
		/// <param name="FromTime"></param>
		/// <param name="smallestToDisplay">The lowest value that gets calculated.  For example, if set to Minutes, then seconds (and milliseconds) will never get shown.</param>
		/// <param name="TruncationOption">Determines how much info is returned</param>
		/// <param name="Abbreviate">Example: "1d 2h 3m 4s 5ms" or "1 day 2 hours 3 minutes 4 seconds 5 milliseconds"</param>
		/// <param name="largestToDisplay">Example: If largest = Hours, then "3d 12h ..." = "84h ..."</param>
		/// <param name="IfZeroIncludeLessThan">Example: If largest = Hours and FromTime = 1 minute, then returns "less than 1 hour"</param>
		public static string ToTimeString(TimeSpan FromTime, FormattingOptions formattingOptions)
		{
			string ret = "";
			bool TextStarted = false;
			if (formattingOptions.LargestUnitToDisplay < formattingOptions.SmallestUnitToDisplay) {
				formattingOptions.LargestUnitToDisplay = formattingOptions.SmallestUnitToDisplay;
			}
			// Do some rounding if necessary:
			bool RoundDown = formattingOptions.IfZeroIncludeLessThan;
			switch (formattingOptions.SmallestUnitToDisplay) {
				case AccuracyOptions.Weeks:
					if (RoundDown) {
						FromTime = TimeSpan.FromDays(Math.Floor(FromTime.TotalDays / 7) * 7);
					} else {
						FromTime = TimeSpan.FromDays(Math.Ceiling(FromTime.TotalDays / 7) * 7);
					}
					break;
				case AccuracyOptions.Days:
					if (RoundDown) {
						FromTime = TimeSpan.FromDays(Math.Floor(FromTime.TotalDays));
					} else {
						FromTime = TimeSpan.FromDays(Math.Ceiling(FromTime.TotalDays));
					}
					break;
				case AccuracyOptions.Hours:
					if (RoundDown) {
						FromTime = TimeSpan.FromHours(Math.Floor(FromTime.TotalHours));
					} else {
						FromTime = TimeSpan.FromHours(Math.Ceiling(FromTime.TotalHours));
					}
					break;
				case AccuracyOptions.Minutes:
					if (RoundDown) {
						FromTime = TimeSpan.FromMinutes(Math.Floor(FromTime.TotalMinutes));
					} else {
						FromTime = TimeSpan.FromMinutes(Math.Ceiling(FromTime.TotalMinutes));
					}
					break;
				case AccuracyOptions.Seconds:
					if (RoundDown) {
						FromTime = TimeSpan.FromSeconds(Math.Floor(FromTime.TotalSeconds));
					} else {
						FromTime = TimeSpan.FromSeconds(Math.Ceiling(FromTime.TotalSeconds));
					}
					break;
				case AccuracyOptions.Milliseconds:
					if (RoundDown) {
						FromTime = TimeSpan.FromMilliseconds(Math.Floor(FromTime.TotalMilliseconds));
					} else {
						FromTime = TimeSpan.FromMilliseconds(Math.Ceiling(FromTime.TotalMilliseconds));
					}
					break;
			}

			for (AccuracyOptions i = formattingOptions.LargestUnitToDisplay; i >= formattingOptions.SmallestUnitToDisplay; i += -1) {
			    double Value = 0;
                string TimeTitle = "";
				switch (i) {
					case AccuracyOptions.Weeks:
						Value = Math.Floor(FromTime.TotalDays / 7);
						TimeTitle = (formattingOptions.Abbreviate ? "w" : " week");
						FromTime -= TimeSpan.FromDays(Value * 7);
						break;
					case AccuracyOptions.Days:
						Value = Math.Floor(FromTime.TotalDays);
						TimeTitle = (formattingOptions.Abbreviate ? "d" : " day");
						FromTime -= TimeSpan.FromDays(Value);
						break;
					case AccuracyOptions.Hours:
						Value = Math.Floor(FromTime.TotalHours);
						TimeTitle = (formattingOptions.Abbreviate ? "h" : " hour");
						FromTime -= TimeSpan.FromHours(Value);
						break;
					case AccuracyOptions.Minutes:
						Value = Math.Floor(FromTime.TotalMinutes);
						TimeTitle = (formattingOptions.Abbreviate ? "m" : " minute");
						FromTime -= TimeSpan.FromMinutes(Value);
						break;
					case AccuracyOptions.Seconds:
						Value = Math.Floor(FromTime.TotalSeconds);
						TimeTitle = (formattingOptions.Abbreviate ? "s" : " second");
						FromTime -= TimeSpan.FromSeconds(Value);
						break;
					case AccuracyOptions.Milliseconds:
						Value = Math.Floor(FromTime.TotalMilliseconds);
						TimeTitle = (formattingOptions.Abbreviate ? "ms" : " millisecond");
						FromTime -= TimeSpan.FromMilliseconds(Value);
						break;
				}


				//Determine whether to display this value
				bool DisplayThisValue = false;
				switch (formattingOptions.TruncationOption) {
					case TruncationOptions.Auto:
						if (Value > 0)
							DisplayThisValue = true;
						break;
					case TruncationOptions.Shortest:
						if (TextStarted)
							break; // TODO: might not be correct. Was : Exit For

						if (Value > 0)
							DisplayThisValue = true;
						break;
					case TruncationOptions.Fill:
						if (TextStarted | Value > 0)
							DisplayThisValue = true;
						break;
					case TruncationOptions.Full:
						DisplayThisValue = true;
						break;
				}

				//we need to display SOMETHING (even if it's zero)
				if (i == formattingOptions.SmallestUnitToDisplay & TextStarted == false) {
					DisplayThisValue = true;
					if (formattingOptions.IfZeroIncludeLessThan & Value == 0){ret += "less than ";Value = 1;}
				}

				if (DisplayThisValue) {
					ret += Value + TimeTitle;
					if (Value != 1 & !formattingOptions.Abbreviate)
						ret += "s";
					ret += " ";
					TextStarted = true;
				}
			}

			return ret.Trim();
		}


        
        static Regex static_TimeString_r = new Regex("\\b(w|d|h|m|s|ms|auto|short|fill|full|abbr|noabbr|less|noless)\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        
        /// <summary>
		/// Converts the Timespan into a string, using the format options as a shortcut.
		/// Example:
		/// ts$ = TimeString(Now.TimeOfDay, "[(smallest)w|d|h|m|s|ms] [(largest)w|d|h|m|s|ms] [auto|short|fill|full] [abbr|noabbr]")
		/// </summary>
		/// <param name="FromTime"></param>
		/// <param name="formatOptions">A list of flags options.
		/// Syntax:
		/// [(smallest)w|d|h|m|s|ms] [(largest)w|d|h|m|s|ms] [auto|short|fill|full] [abbr|noabbr] [less|noless]
		/// </param>
		/// <remarks> The format options are case insensitive. </remarks>
		public static string ToTimeString(TimeSpan FromTime, string formatOptions, FormattingOptions defaultFormattingOptions)
		{
			formatOptions = formatOptions.ToLower();

			TruncationOptions TruncationOption = defaultFormattingOptions.TruncationOption;
			bool Abbreviate = defaultFormattingOptions.Abbreviate;
		    bool IfZeroIncludeLessThan = defaultFormattingOptions.IfZeroIncludeLessThan;

			AccuracyOptions LargestToDisplay = AccuracyOptions.Milliseconds - 1;
			AccuracyOptions SmallestToDisplay = AccuracyOptions.Weeks + 1;

			AccuracyOptions? newRange = null;
			foreach (Match m in static_TimeString_r.Matches(formatOptions)) {
				switch (m.Value) {
					case "w":
						newRange = AccuracyOptions.Weeks;
						break;
					case "d":
						newRange = AccuracyOptions.Days;
						break;
					case "h":
						newRange = AccuracyOptions.Hours;
						break;
					case "m":
						newRange = AccuracyOptions.Minutes;
						break;
					case "s":
						newRange = AccuracyOptions.Seconds;
						break;
					case "ms":
						newRange = AccuracyOptions.Milliseconds;

						break;
					case "auto":
						TruncationOption = TruncationOptions.Auto;
						break;
					case "short":
						TruncationOption = TruncationOptions.Shortest;
						break;
					case "fill":
						TruncationOption = TruncationOptions.Fill;
						break;
					case "full":
						TruncationOption = TruncationOptions.Full;

						break;
					case "abbr":
						Abbreviate = true;
						break;
					case "noabbr":
						Abbreviate = false;

						break;
					case "less":
						IfZeroIncludeLessThan = true;
						break;
					case "noless":
						IfZeroIncludeLessThan = false;
						break;
				}

				if (newRange.HasValue) {
                    if (SmallestToDisplay > newRange.Value) {
                        SmallestToDisplay = newRange.Value;
                    }
                    if (LargestToDisplay < newRange.Value) {
                        LargestToDisplay = newRange.Value;
                    }
				}
			}

			if (!newRange.HasValue) {
				//let's do defaults:
				SmallestToDisplay = defaultFormattingOptions.SmallestUnitToDisplay;
				LargestToDisplay = defaultFormattingOptions.LargestUnitToDisplay;
			}

            return ToTimeString(FromTime, 
                new FormattingOptions() {
                                            SmallestUnitToDisplay = SmallestToDisplay, 
                                            LargestUnitToDisplay = LargestToDisplay, 
                                            TruncationOption = TruncationOption, 
                                            Abbreviate = Abbreviate, 
                                            IfZeroIncludeLessThan = IfZeroIncludeLessThan
                                        });
		}

    
    }
}
