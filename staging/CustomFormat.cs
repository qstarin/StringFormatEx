using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;



public sealed partial class _CustomFormat
{
    static _CustomFormat()
    {
        _CustomFormat.ExtendCustomFormat += _CustomFormat._GetDefaultOutput;
        _CustomFormat.ExtendCustomFormat += _CustomFormat.FormatConditional;
        _CustomFormat.ExtendCustomFormat += _CustomFormat.Do_Array_Formatting;

        _CustomFormat.ExtendCustomSource += _CustomFormat._GetDefaultSource;
        _CustomFormat.ExtendCustomSource += _CustomFormat.Get_Array_Source;
    }

    #region Shared Fields

    /// <summary>
    /// The character that escapes other characters
    /// </summary>
    public static char escapeCharacter = '\\';
    
    /// <summary>
    /// A string that contains all the special escape characters.
    /// The order of these characters matches the order of the escapeText.
    /// </summary>
    public static string escapeCharacters = "nt{}\\";
    
    public static string[] escapeText = new string[] { "\r\n", "\t", "{", "}", "\\" };
    
    /// <summary>
    /// A string containing Selector split characters.
    /// Any of these characters chain together properties.
    /// For example, {Person.Address.City}.
    /// 
    /// Two things to notice:
    /// Multiple splitters in a row are ignored;
    /// Parenthesis/brackets do NOT have to be matched, and they do NOT affect order of operations.
    /// 
    /// Therefore, the following examples are identical:
    ///  {Person.Address.City} 
    ///  {Person)Address]City}
    ///  {[Person]Address(City)}
    ///  {..Person...Address...City..}
    ///  {..Person(Address)]]]City)[)..}
    /// 
    /// This allows comfortable syntaxes such as {Array[0].Item}
    /// This syntax is identical to {Array.0.Item} and {Array]0]Item} and {[Array][0](Item)}
    /// 
    /// </summary>
    public static string selectorSplitters = ".[]()";
    
    public static string selectorCharacters = "_";

    #endregion


    #region CustomFormat overloads

    /// <summary>
    /// Formats the format string using the parameters provided.
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public static string CustomFormat(string format, params object[] args) {
        StringWriter output = new StringWriter(new StringBuilder((format.Length * 2)));
        //  Guessing a length can help performance a little.
        CustomFormat(new CustomFormatInfo(output, format, args));
        return output.ToString();
    }
    
    /// <summary>
    /// Performs the CustomFormat and outputs to a Stream.
    /// </summary>
    public static void CustomFormat(Stream output, string format, params object[] args) {
        CustomFormat(new CustomFormatInfo(new StreamWriter(output), format, args));
    }
    
    /// <summary>
    /// Performs the CustomFormat and outputs to a TextWriter.
    /// </summary>
    /// <param name="output">Common types of TextWriters are StringWriter and StreamWriter.</param>
    public static void CustomFormat(TextWriter output, string format, params object[] args) {
        CustomFormat(new CustomFormatInfo(output, format, args));
    }

#endregion

    
    #region CustomFormat
    
    /// <summary>
    /// Does the actual work.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void CustomFormat(CustomFormatInfo info) {
        if (info.Current == null && info.Arguments.Length >= 1) {
            info.Current = info.Arguments[0];
        }

        //  We need to store the Format and the Current items and keep them in this context
        string format = info.Format;
        object current = info.Current;


        // ' Here is the regular expression to use for parsing the Format string:
        // Static R As New Regex( _
        //   "{  ([0-9A-Za-z_.\[\]()]*)   (?:    :  ( (?:    (?<open>{)     |     (?<nest-open>})     |     [^{}]+     )*? ) (?(open)(?!))  )?  }" _
        //   , RegexOptions.IgnorePatternWhitespace Or RegexOptions.Compiled)
        //   {  (      Selectors     )   (Optnl :           {  Nested                         }     or      Format                         )   }


        int lastAppendedIndex = 0;
        PlaceholderInfo placeholder = null;

        while (NextPlaceholder(format, lastAppendedIndex, format.Length, ref placeholder)) {
            //  Write the text in-between placeholders:
            info.WriteRegularText(format, lastAppendedIndex, (placeholder.placeholderStart - lastAppendedIndex));
            lastAppendedIndex = placeholder.placeholderStart + placeholder.placeholderLength;

            //  Evaluate the source by evaluating each argSelector:
            info.Current = current; //  Restore the current scope
            //bool isFirstSelector = true; // TODO: Remove this variable if it never gets used again
            int selectorIndex = -1;

            foreach (string selector in placeholder.selectors) {
                selectorIndex++;
                info.SetSelector(selector, selectorIndex);
                //  Raise the ExtendCustomSource event to allow custom source evaluation:
                OnExtendCustomSource(info);

                //  Make sure that the selector has been handled:
                if (!info.Handled) {
                    break;
                }
                //isFirstSelector = false;
            }

            //  Handle errors:
            if (!info.Handled) {
                //  If the ExtendCustomSource event wasn't handled,
                //  then the Selector could not be evaluated.
                if (!OnInvalidSelector(format, info, placeholder)) {
                    continue;
                }
            }

            string argFormat = format.Substring(placeholder.formatStart, placeholder.formatLength);
            info.SetFormat(argFormat, placeholder.hasNested);

            try {
                //  Raise the ExtendCustomFormat event to allow custom formatting:
                OnExtendCustomFormat(info);
            } 
            catch (Exception ex) {
                //  Handle errors:
                OnInvalidFormat(format, info, placeholder, ex);
            }
            //  Write the substring between the last bracket and the end of the string:
            info.WriteRegularText(format, lastAppendedIndex, (format.Length - lastAppendedIndex));
        }
    }

    #endregion
    

    #region NextPlaceholder

    /// <summary>
    /// Returns True if the placeholder was formatted correctly; False if a placeholder couldn't be found.
    /// Outputs all relevant placeholder information.
    /// 
    /// This function takes the place of the Regular Expression.
    /// It is faster and more direct, and does not suffer from Regex endless loops.
    /// In tests, this nearly doubles the speed vs Regex.
    /// </summary>
    public static bool NextPlaceholder(string format, int startIndex, int endIndex, ref PlaceholderInfo placeholder) {
        placeholder = new PlaceholderInfo();
        placeholder.hasNested = false;
        placeholder.placeholderStart = -1;
        placeholder.selectorLength = -1;
        IList<string> selectorSplitList = new List<string>();

        int lastSplitIndex = 0;
        int openCount = 0;
        // Dim endIndex% = format.Length

        while (startIndex < endIndex) {
            char c = format[startIndex];
            if (placeholder.placeholderStart == -1) {
                //  Looking for "{"
                if (c == '{') {
                    placeholder.placeholderStart = startIndex;
                    placeholder.selectorStart = startIndex + 1;
                    lastSplitIndex = placeholder.selectorStart;
                }
                else if (c == escapeCharacter) {
                    //  The next character is escaped
                    startIndex++;
                }
            }
            else if (placeholder.selectorLength == -1) {
                //  Looking for ":" or "}" ...
                //  or an alpha-numeric or a selectorSplitter
                if (c == '}') {
                    //  Add this item to the list of Selectors (as long as it isn't empty)
                    if (lastSplitIndex < startIndex) {
                        selectorSplitList.Add(format.Substring(lastSplitIndex, (startIndex - lastSplitIndex)));
                        lastSplitIndex = (startIndex + 1);
                    }
                    placeholder.selectors = selectorSplitList.ToArray();
                    placeholder.placeholderLength = startIndex + 1 - placeholder.placeholderStart;
                    placeholder.selectorLength = startIndex - placeholder.selectorStart;
                    placeholder.formatLength = 0;
                    return true;
                }
                else if (c == ':') {
                    //  Add this item to the list of Selectors (as long as it isn't empty)
                    if (lastSplitIndex < startIndex) {
                        selectorSplitList.Add(format.Substring(lastSplitIndex, (startIndex - lastSplitIndex)));
                        lastSplitIndex = startIndex + 1;
                    }
                    placeholder.selectors = selectorSplitList.ToArray();
                    placeholder.selectorLength = startIndex - placeholder.selectorStart;
                    placeholder.formatStart = startIndex + 1;
                }
                else if (selectorSplitters.IndexOf(c) >= 0) {
                    //  It is a valid splitter character
                    //  Add this item to the list of Selectors (as long as it isn't empty)
                    if (lastSplitIndex < startIndex) {
                        selectorSplitList.Add(format.Substring(lastSplitIndex, (startIndex - lastSplitIndex)));
                        lastSplitIndex = (startIndex + 1);
                    }
                }
                else if (char.IsLetterOrDigit(c) || selectorCharacters.Contains(c)) {
                    //  It is a valid selector character, so let's just continue
                }
                else {
                    //  It is NOT a valid character!!!
                    if (placeholder.selectorStart <= startIndex) {
                        startIndex--;
                    }
                    placeholder.placeholderStart = -1; //  Restart the search
                    selectorSplitList.Clear();
                }
            }
            else {
                //  We are in the Format section:
                //  Looking for a "}"
                if (c == '}') {
                    if (openCount == 0) {
                        // we're done!
                        placeholder.placeholderLength = startIndex + (1 - placeholder.placeholderStart);
                        placeholder.formatLength = startIndex - placeholder.formatStart;
                        return true;
                    }
                    else {
                        openCount--;
                    }
                }
                else if (c == '{') {
                    //  It's a nested bracket
                    openCount++;
                    placeholder.hasNested = true;
                }
                else {
                    //  It's just part of the Format
                }
            }
            startIndex++;
        }
        return false;
    }

    #endregion


    #region ExtendCustomSource event

    public delegate void ExtendCustomSourceDelegate(ICustomSourceInfo info);

    public static void OnExtendCustomSource(ICustomSourceInfo info)
    {
        if (CustomSourceHandlers != null) {
            foreach (var list in CustomSourceHandlers.Values) {
                foreach (var handler in list) {
                    if (info.Handled) {
                        return;
                    }
                    handler.Invoke(info);
                }
            }
        }
    }

    private static IDictionary<CustomFormatPriorities, IList<ExtendCustomSourceDelegate>> CustomSourceHandlers;
    public static event ExtendCustomSourceDelegate ExtendCustomSource {
        add
        {
            if (CustomSourceHandlers == null) {
                CustomSourceHandlers = new SortedDictionary<CustomFormatPriorities, IList<ExtendCustomSourceDelegate>>();
            }

            //  Let's search for the "CustomFormatPriorityAttribute" to see if we should add this handler at a higher priority in the handler list:
            CustomFormatPriorities handlerPriority = CustomFormatPriorities.Normal;
            //  default priority
            foreach (CustomFormatPriorityAttribute pa in value.Method.GetCustomAttributes(typeof(CustomFormatPriorityAttribute), true)) {
                handlerPriority = pa.Priority;
                //  There should never be more than 1 PriorityAttribute
            }
            //  Make sure there is a list for this priority:
            if (!CustomSourceHandlers.ContainsKey(handlerPriority)) {
                CustomSourceHandlers.Add(handlerPriority, new List<ExtendCustomSourceDelegate>());
            }
            //  Add the new handler to the list:
            CustomSourceHandlers[handlerPriority].Add(value);
        }

        remove
        {
            if (CustomSourceHandlers != null) {
                foreach (var list in CustomSourceHandlers.Values) {
                    if (list.Remove(value)) {
                        return;
                    }
                }
            }
        }
    }

    #endregion


    #region ExtendCustomFormat

    public delegate void ExtendCustomFormatDelegate(CustomFormatInfo info);

    public static void OnExtendCustomFormat(CustomFormatInfo info)
    {
        if (CustomFormatHandlers != null) {
            foreach (var list in CustomFormatHandlers.Values) {
                foreach (var handler in list) {
                    if (info.Handled) {
                        return;
                    }
                    handler.Invoke(info);
                }
            }
        }
    }


    static private IDictionary<CustomFormatPriorities, IList<ExtendCustomFormatDelegate>> CustomFormatHandlers;

    /// <summary>
    /// An event that allows custom formatting to occur.
    /// 
    /// Why is it Custom?  2 reasons:
    /// " This event short-circuits if a handler sets the output, to improve efficiency and redundancy
    /// " This event allows you to set an "event priority" by applying the CustomFormatPriorityAttribute to the handler method!  (See "How to add your own Custom Formatter:" below)
    /// 
    /// This adds a little complexity to the event, but there are occassions when we need the CustomFormat handlers to execute in a certain order.
    /// </summary>
    public static event ExtendCustomFormatDelegate ExtendCustomFormat
    {
        add
        {
            if (CustomFormatHandlers == null) {
                //  Initialize the event dictionary:
                CustomFormatHandlers = new SortedDictionary<CustomFormatPriorities, IList<ExtendCustomFormatDelegate>>();
            }

            //  Let's search for the "CustomFormatPriorityAttribute" to see if we should add this handler at a higher priority in the handler list:
            CustomFormatPriorities handlerPriority = CustomFormatPriorities.Normal; //  default priority

            foreach (CustomFormatPriorityAttribute pa in value.Method.GetCustomAttributes(typeof(CustomFormatPriorityAttribute), true)) {
                handlerPriority = pa.Priority;
                //  There should never be more than 1 PriorityAttribute
            }

            //  Make sure there is a list for this priority:
            if (!CustomFormatHandlers.ContainsKey(handlerPriority)) {
                CustomFormatHandlers.Add(handlerPriority, new List<ExtendCustomFormatDelegate>());
            }

            //  Add the new handler to the list:
            CustomFormatHandlers[handlerPriority].Add(value);
        }

        remove
        {
            if (CustomFormatHandlers != null) {
                foreach (var list in CustomFormatHandlers.Values) {
                    if (list.Remove(value)) {
                        return;
                    }
                }
            }
        }
    }

    #endregion


    #region GetDefaultSource

    /// <summary>
    /// This is the Default method for evaluating the Source.
    /// 
    /// 
    /// If this is the first selector and the selector is an integer, then it returns the (global) indexed argument (just like String.Format).
    /// If the Current item is a Dictionary that contains the Selector, it returns the dictionary item.
    /// Otherwise, Reflection will be used to determine if the Selector is a Property, Field, or Method of the Current item.
    /// </summary>
    [CustomFormatPriority(CustomFormatPriorities.Low)]
    public static void _GetDefaultSource(ICustomSourceInfo info) 
    {
        //  If it wasn't handled, let's evaluate the source on our own:
        //  We will see if it's an argument index, dictionary key, or a property/field/method.
        //  Maybe source is the global index of our arguments? 
        int argIndex;
        if (info.SelectorIndex == 0 && int.TryParse(info.Selector, out argIndex)) {
            if (argIndex < info.Arguments.Length) {
                info.Current = info.Arguments[argIndex];
            }
            else {
                //  The index is out-of-range!
            }
            return;
        }

        //  Maybe source is a Dictionary?
        if (info.Current is IDictionary && ((IDictionary)info.Current).Contains(info.Selector)) {
            info.Current = ((IDictionary)info.Current)[info.Selector];
            return;
        }


        // REFLECTION:
        // Let's see if the argSelector is a Property/Field/Method:
        var sourceType = info.Current.GetType();
        MemberInfo[] members = sourceType.GetMember(info.Selector);
        foreach (MemberInfo member in members) {
            switch (member.MemberType) {
                case MemberTypes.Field:
                    //  Selector is a Field; retrieve the value:
                    FieldInfo field = member as FieldInfo;
                    info.Current = field.GetValue(info.Current);
                    return;
                case MemberTypes.Property:
                case MemberTypes.Method:
                    MethodInfo method;
                    if (member.MemberType == MemberTypes.Property) {
                        //  Selector is a Property
                        PropertyInfo prop = member as PropertyInfo;
                        //  Make sure the property is not WriteOnly:
                        if (prop.CanRead) {
                            method = prop.GetGetMethod();
                        }
                        else {
                            continue;
                        }
                    }
                    else {
                        //  Selector is a Method
                        method = member as MethodInfo;
                    }

                    //  Check that this method is valid -- it needs to be a Function (return a value) and has to be parameterless:
                    //  We are only looking for a parameterless Property/Method:
                    if ((method.GetParameters().Length > 0)) {
                        continue;
                    }

                    //  Make sure that this method is not a Sub!  It has to be a Function!
                    if ((method.ReturnType == typeof(void))) {
                        continue;
                    }

                    //  Retrieve the Property/Method value:
                    info.Current = method.Invoke(info.Current, new object[0]);
                    return;
            }
        }
        //  If we haven't returned yet, then the item must be invalid.
    }

    #endregion


    #region GetDefaultOutput

    /// <summary>
    /// This is the Default method for formatting the output.
    /// This code has been derived from the built-in String.Format() function.
    /// </summary>
    [CustomFormatPriority(CustomFormatPriorities.Low)]
    public static void _GetDefaultOutput(CustomFormatInfo info) {
        //  Let's see if there are nested items:
        if (info.HasNested) {
            info.CustomFormatNested();
            return;
        }
        //  Let's do the default formatting:
        //  We will try using IFormatProvider, IFormattable, and if all else fails, ToString.
        //  (This code was adapted from the built-in String.Format code)
        if (info.Provider != null) {
            //  Use the provider to see if a CustomFormatter is available:
            ICustomFormatter formatter = info.Provider.GetFormat(typeof(ICustomFormatter)) as ICustomFormatter;
            if (formatter != null) {
                info.Write(formatter.Format(info.Format, info.Current, info.Provider));
                return;
            }
            //  Now try to format the object, using its own built-in formatting if possible:
            if (info.Current.GetType() is IFormattable) {
                info.Write(((IFormattable)info.Current).ToString(info.Format, info.Provider));
            }
            else {
                info.Write(info.Current.ToString());
            }
        }
    }

    #endregion


    #region ErrorActions

    public enum ErrorActions { ThrowError, OutputErrorInResult, Ignore }

#if DEBUG
    //  Makes it easier to spot errors while debugging.
    /// <summary>
    /// Determines what to do if a Format string cannot be successfully evaluated.
    /// </summary>
    public static ErrorActions InvalidSelectorAction = ErrorActions.ThrowError;
    public static ErrorActions InvalidFormatAction = ErrorActions.ThrowError;
#else
    /// <summary>
    /// Determines what to do if a Format string cannot be successfully evaluated.
    /// </summary>
    public static ErrorActions InvalidSelectorAction = ErrorActions.OutputErrorInResult;
    public static ErrorActions InvalidFormatAction = ErrorActions.OutputErrorInResult;
#endif

    #endregion


    #region OnInvalidSelector

    /// <summary>
    /// Determines what to do when an Invalid Selector is found.
    /// 
    /// Returns True if we should just continue; False if we should skip this item.
    /// </summary>
    private static bool OnInvalidSelector(string format, CustomFormatInfo info, PlaceholderInfo placeholder)
    {
        string invalidSelector = format.Substring(placeholder.selectorStart, placeholder.selectorLength);

        string message;
        switch (InvalidSelectorAction) {
            case ErrorActions.ThrowError:
                //  Let's give a detailed description of the error:
                message = CustomFormat(
                        ("Invalid Format String.\\n" +
                         ("Could not evaluate \"{0}\": \"{1}\" is not a member of {2}.\\n" +
                          ("The error occurs at position {3} of the following format string:\\n" + "{4}"))),
                        invalidSelector, info.Selector, info.CurrentType, placeholder.placeholderStart, format);
                throw new ArgumentException(message, invalidSelector);
            case ErrorActions.OutputErrorInResult:
                //  Let's put the placeholder back,
                //  along with the error.
                //  Example: {Person.Name.ABC}  becomes  {Person.Name.ABC:(Error: "ABC" is not a member of String)}
                message = ("{" + (CustomFormat("{0}:(Error: \"{1}\" is not a member of {2})", invalidSelector,
                                                info.Selector, info.CurrentType) + "}"));
                info.WriteError(message, placeholder);
                return false;
            case ErrorActions.Ignore:
                //  Allow formatting to continue!
                break;
        }
        return true;
    }

    #endregion


    #region OnInvalidFormat

    /// <summary>
    /// Determines what to do when an Invalid Selector is found.
    /// </summary>
    private static void OnInvalidFormat(string format, CustomFormatInfo info, PlaceholderInfo placeholder, Exception ex)
    {
        string selector = format.Substring(placeholder.selectorStart, placeholder.selectorLength);
        string invalidFormat = format.Substring(placeholder.formatStart, placeholder.formatLength);
        string errorMessage = ex.Message;

        if (ex.GetType() is FormatException) {
            errorMessage = CustomFormat("\"{0}\" is not a valid format specifier for {1}", invalidFormat,
                                        info.CurrentType);
        }

        string message;
        switch (InvalidFormatAction) {
            case ErrorActions.ThrowError:
                //  Let's give a detailed description of the error:
                message = CustomFormat(
                        ("Invalid Format String.\\n" +
                         ("Could not evaluate {{0}} because {1}.\\n" +
                          ("The error occurs at position {2} of the following format string:\\n" + "{3}"))), selector,
                        errorMessage, placeholder.placeholderStart, format);
                throw new ArgumentException(message, invalidFormat, ex);
                break;
            case ErrorActions.OutputErrorInResult:
                //  Let's put the placeholder back,
                //  along with the error.
                //  Example: {Person.Birthday:x}  becomes  {Person.Birthday:(Error: "x" is an invalid format specifier)}
                message = ("{" + (CustomFormat("{0}:(Error: {1})", selector, errorMessage) + "}"));
                info.WriteError(message, placeholder);
                break;
            case ErrorActions.Ignore:
                //  Allow formatting to continue!
                break;
        }
    }

    #endregion


    #region SplitNested

    /// <summary>
    /// Enhances String.Split by ignoring any characters that are between the nested characters.
    /// It also allows you to stop after a certain number of splits.
    /// 
    /// Example:
    /// SplitNested("a|b{1|2|3}|c", "|"c) = {"a", "b{1|2|3}", "c"}
    /// SplitNested("a|b{1|2|3}|c", "|"c, 2) = {"a", "b{1|2|3}|c"}
    /// 
    /// </summary>
    protected static string[] SplitNested(string format, char splitChar)
    {
        return SplitNested(format, splitChar, 0);
    }

    protected static string[] SplitNested(string format, char splitChar, int maxItems)
    {
        int openCount = 0;
        IList<string> items = new List<String>(4); //  (Estimating 4 matches)
        int lastSplit = 0;

        for (int i = 0; i < format.Length; i++) {
            char c = format[i];
            if (c == '{') {
                openCount++;
            } 
            else if (c == '}') {
                openCount--;
            } 
            else if (c == splitChar && openCount == 0) {
                items.Add(format.Substring(lastSplit, (i - lastSplit)));
                lastSplit = i + 1;
                if (maxItems != 0 && items.Count == (maxItems - 1)) {
                    break;
                }
            }
        }
        items.Add(format.Substring(lastSplit));
        return items.ToArray();
    }

    #endregion

}
