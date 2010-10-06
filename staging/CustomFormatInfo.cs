﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;


public partial class _CustomFormat
{
    /// <summary>
    /// Contains all the data necessary to perform a CustomFormat.
    /// This class is split into "Source" and "Format" sections because it takes care of both functions.
    /// </summary>
    public class CustomFormatInfo : ICustomSourceInfo
    {
        #region "      Constructors "

        /// <summary>
        /// This method should only be used internally.
        /// </summary>
        public CustomFormatInfo(TextWriter newOutput, IFormatProvider newProvider, string newFormat, object[] newArgs)
        {
            this.Output = newOutput;
            if (Output is StringWriter) {
                // Let's use the underlying StringBuilder for better performance
                OutputSB = ((StringWriter)Output).GetStringBuilder();
            }
            this.mFormat = newFormat;
            this.mArguments = newArgs;
            this.mProvider = newProvider;
        }


        /// <summary>
        /// This method should only be used internally.
        /// </summary>
        public CustomFormatInfo(TextWriter newOutput, string newFormat, object[] newArgs)
            : this(newOutput, null, newFormat, newArgs)
        {
        }

        #endregion

        #region "      Custom Source Properties "

        /// <summary>
        /// This method should only be used internally.
        /// 
        /// Updates the Selector for use in the ExtendCustomSource event.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public void SetSelector(string newSelector, int newSelectorIndex)
        {
            this.mSelector = newSelector;
            this.mSelectorIndex = newSelectorIndex;

            this.mHandled = false;
        }

        private string mSelector;
        /// <summary>
        /// This property is hidden, because it is only used as an argument to CustomSourceEventArgs.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string Selector
        {
            get { return mSelector; }
        }

        private int mSelectorIndex;
        /// <summary>
        /// This property is hidden, because it is only used as an argument to CustomSourceEventArgs.
        /// </summary>
        public int SelectorIndex
        {
            get { return this.mSelectorIndex; }
        }

        private readonly object[] mArguments;
        /// <summary>
        /// An array of all the original arguments passed to the CustomFormat function.
        /// This is not used often, but provides "global" access to these objects.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public object[] Arguments
        {
            get { return mArguments; }
        }

        private object mCurrent;
        /// <summary>
        /// This is the current item, as chosen by the "selector", that should be formatted.
        /// In the example "{0.Date.Year:N4}", the current item is "0.Date.Year" and the Format is "N4".
        /// 
        /// You shouldn't change this item.
        /// </summary>
        public object Current
        {
            get { return mCurrent; }
            set
            {
                this.mHandled = true;
                mCurrent = value;
                this.cache.Clear();
            }
        }

        #endregion

        #region "      Custom Format Properties "

        /// <summary>
        /// This method should only be used internally.
        /// 
        /// Updates the Format property for use in a ExtendCustomFormat event.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public void SetFormat(string newFormat, bool newHasNested)
        {
            this.mFormat = newFormat;
            this.mHasNested = newHasNested;

            this.mHandled = false;
        }
        private string mFormat;
        /// <summary>
        /// This is the optional format string that occurs after the colon.
        /// 
        /// In the example "{0.Date.Year:N4}", the current item is "0.Date.Year" and the Format is "N4".
        /// </summary> 
        public string Format
        {
            get { return mFormat; }
        }

        private bool mHasNested;
        /// <summary>
        /// Returns True if the Format has any nested {curly braces}
        /// </summary>
        public bool HasNested
        {
            get { return mHasNested; }
        }

        private bool mHandled = false;
        /// <summary>
        /// Determines if CustomSource or CustomFormat events should continue to fire.
        /// Automatically gets set to True when the "Write" method is used.
        /// </summary>
        public bool Handled
        {
            get { return mHandled; }
            set { this.mHandled = value; }
        }

        /// <summary>
        /// The (optional) FormatProvider that provides culture-specific formats.
        /// 
        /// However, this item is usually Nothing; it is only implemented because the original String.Format implements it, and I wanted to be compatible.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        private readonly IFormatProvider mProvider;
        public IFormatProvider Provider
        {
            get { return mProvider; }
        }

        #endregion

        #region "      Helper properties - cache common calls to help determine the Current type "

        protected IDictionary<string, object> cache = new Dictionary<string, object>();
        /// <summary>
        /// Returns the Current item's type.
        /// Caches the type for better performance.
        /// If Item is Nothing, returns Nothing.
        /// </summary>
        public Type CurrentType
        {
            get
            {
                const string cacheKey = "itemType";
                if (this.mCurrent == null) {
                    return null;
                }
                if (!cache.ContainsKey(cacheKey)) {
                    cache.Add(cacheKey, this.mCurrent.GetType());
                }
                return (Type)cache[cacheKey]; // TODO: Remove cast
            }
        }
        /// <summary>
        /// True if Current is an Integer, Long, Short, Single, Double, Decimal, or Enum.
        /// </summary>
        public bool CurrentIsNumber
        {
            get
            {
                const string cacheKey = "isNumber";
                if (!cache.ContainsKey(cacheKey)) {
                    cache.Add(cacheKey, mCurrent is int || mCurrent is long || mCurrent is short || mCurrent is float || mCurrent is double || mCurrent is decimal || (mCurrent != null && this.CurrentType.IsEnum));
                }
                return (bool)cache[cacheKey]; // TODO: Remove cast
            }
        }
        /// <summary>
        /// True if Current is a Single, Double, or Decimal.
        /// </summary>
        public bool CurrentIsFloat
        {
            get
            {
                const string cacheKey = "isFloat";
                if (!cache.ContainsKey(cacheKey)) {
                    cache.Add(cacheKey, mCurrent is float || mCurrent is double || mCurrent is decimal);
                }
                return (bool)cache[cacheKey]; // TODO: remove cast
            }
        }
        /// <summary>
        /// True if Current is a Date.
        /// </summary>
        public bool CurrentIsDate
        {
            get
            {
                const string cacheKey = "isDate";
                if (!cache.ContainsKey(cacheKey)) {
                    cache.Add(cacheKey, mCurrent is System.DateTime);
                }
                return (bool)cache[cacheKey]; // TODO: remove cast
            }
        }
        /// <summary>
        /// True if Current is a Boolean.
        /// </summary>
        public bool CurrentIsBoolean
        {
            get
            {
                const string cacheKey = "isBoolean";
                if (!cache.ContainsKey(cacheKey)) {
                    cache.Add(cacheKey, mCurrent is bool);
                }
                return (bool)cache[cacheKey]; // TODO: remove cast
            }
        }
        /// <summary>
        /// True if Current is a String.
        /// </summary>
        public bool CurrentIsString
        {
            get
            {
                const string cacheKey = "isString";
                if (!cache.ContainsKey(cacheKey)) {
                    cache.Add(cacheKey, mCurrent is string);
                }
                return (bool)cache[cacheKey]; // TODO: remove cast
            }
        }
        /// <summary>
        /// True if Current is a TimeSpan.
        /// </summary>
        public bool CurrentIsTimeSpan
        {
            get
            {
                const string cacheKey = "isTimeSpan";
                if (!cache.ContainsKey(cacheKey)) {
                    cache.Add(cacheKey, mCurrent is TimeSpan);
                }
                return (bool)cache[cacheKey]; // TODO: remove cast
            }
        }

        /// <summary>
        /// Checks if the Format starts with a string.
        /// Ignores case.
        /// </summary>
        public bool FormatStartsWith(string value)
        {
            return this.mFormat.StartsWith(value, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Checks if the Format contains a string.
        /// Ignores case.
        /// </summary>
        public bool FormatContains(string value)
        {
            return this.mFormat.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        /// <summary>
        /// Checks if the Format equals a string.
        /// Ignores case.
        /// </summary>
        public bool FormatEquals(string value)
        {
            return this.mFormat.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region "      Output "
        /// <summary>
        /// Writes a string to the output and sets the Handled flag.
        /// 
        /// As an added bonus, automatically escapes the "\t" and "\n" flags
        /// </summary>
        public virtual void Write(string text, int start = 0, int length = -1) // TODO: Replace optional parameters with function overload
        {
            // By default, this doesn't do anything special.
            // However, it can be overridden, in order to monitor the output and customize it.
            this.WriteEscaped(text, start, length);
        }



        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public virtual void WriteLiteral(string text)
        {
            WriteLiteral(text, 0, -1);
        }

        /// <summary>
        /// Writes a string to the output and sets the Handled flag.
        /// 
        /// Does NOT escape any special characters!
        /// This is useful for filenames and such.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public virtual void WriteLiteral(string text, int start, int length)
        {
            this.mHandled = true;
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            if (start < 0 || start > text.Length || start + length > text.Length) {
                throw new ArgumentOutOfRangeException("start", "Start or Length is out of range");
            }
            if (length < 0) {
                length = text.Length - start;
            }
            if (length == 0) {
                return;
            }

            this.WriteFast(text, start, length);
        }




        /// <summary>
        /// Writes a string to the output and sets the Handled flag.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public virtual void WriteError(string message, PlaceholderInfo placeholder)
        {
            // By default, this doesn't do anything special.
            // However, it can be overridden, in order to monitor the output and customize it.
            this.WriteEscaped(message);
        }
        /// <summary>
        /// Writes a string to the output and sets the Handled flag.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public virtual void WriteRegularText(string format, int start, int length)
        {
            // By default, this doesn't do anything special.
            // However, it can be overridden, in order to monitor the output and customize it.
            this.WriteEscaped(format, start, length);
        }

        protected readonly TextWriter Output;
        // If the Output is a StringWriter, then we can increase performance if we use the underlying StringBuilder instead.
        protected readonly StringBuilder OutputSB;



        //public static char escapeCharacter = '\\';
        //public static string escapeCharacters = "nt{}\\";
        //public static string[] escapeText = new string[] { "\r\n", "\t", "{", "}", "\\" };



        protected void WriteEscaped(string text)
        {
            WriteEscaped(text, 0, -1);
        }
        /// <summary>
        /// Writes a string to the output and sets the Handled flag.
        /// 
        /// Effeciently escapes the "\t" and "\n" flags
        /// </summary>
        protected void WriteEscaped(string text, int start, int length)
        {
            this.mHandled = true;
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            if (start < 0 || start > text.Length || start + length > text.Length) {
                throw new ArgumentOutOfRangeException("start", "Start or Length is out of range");
            }
            if (length < 0) {
                length = text.Length - start;
            }
            if (length == 0) {
                return;
            }

            // Try to escape all the \n, \t, \{, \}, and \\ characters:
            // (all these values are contained in _CustomFormat.escapeCharacters)
            // Note: this does NOT escape other characters.  For example, "\f" will output "\f" still.
            int endIndex = start + length;
            int i = start;
            int lastIndex = start;
            int escIndex;
            while (i < endIndex) {
                i = text.IndexOf(escapeCharacter, i, endIndex - i - 1);
                // Find the next "\"
                if (i == -1) {
                    break;
                }
                escIndex = escapeCharacters.IndexOf(text[i + 1]);
                // Figure out what escape character follows the "\"
                i += 2;
                // Skip both characters
                if (escIndex == -1) {
                    continue;
                }
                // The escape character wasn't found.
                WriteFast(text, lastIndex, (i - 2) - lastIndex);
                // Write the text between escapes
                this.Output.Write(escapeText[escIndex]);
                // Write the escaped text
                lastIndex = i;
            }

            WriteFast(text, lastIndex, endIndex - lastIndex);
            // Write the text between escapes

        }
        /// <summary>
        /// Writes a string to the output and sets the Handled flag.
        /// Does not escape any characters.
        /// </summary>
        protected void WriteFast(string text, int start, int length)
        {
            if (length == 0) {
                return;
            }
            // Don't need to write anything

            if (start == 0 && length == text.Length) {
                // Write the whole string; this method doesn't need to be optimized.
                this.Output.Write(text);
            } 
            else if (this.OutputSB != null) {
                // StringWriter Performance Notes:
                //
                // StringBuilder.Append(String, index, length) is FASTER than any TextWriter functions, because
                // it copies directly from the string without having to make a copy.
                // 
                // If our underlying TextWriter is a StringWriter, then we will use the underlying StringWriter.StringBuilder
                // to write the data faster.
                this.OutputSB.Append(text, start, length);
            } else {
                // TextWriter Performance Notes:
                //
                // The TextWriter does not have a function Write(string, index, length).  
                // It only has Write(char(), index, length).
                // Converting a String to Char() creates a copy of the whole string.
                // We can save time by cutting down the string ourselves.
                //
                // There are 2 options for cutting down a string:
                // text.SubString(index, length)    or    text.ToCharArray(index, length)
                //
                // Because of the way a TextWriter is implemented,
                // Write(Char()) could be slightly faster than Write(String) 
                // The performance difference is probably negligible though.
                this.Output.Write(text.ToCharArray(start, length));
            }
        }

        #endregion

        #region "      CustomFormatNested "
        /// <summary>
        /// Performs the CustomFormat method on this object.  
        /// This is the same as calling CustomFormat(Me)
        /// </summary>
        public void CustomFormatNested()
        {
            _CustomFormat.CustomFormat(this);
        }
        #endregion

    }
}