﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;



public sealed class _CustomFormat {
    
    // '' <summary>
    // '' The character that escapes other characters
    // '' </summary>
    public static char escapeCharacter = '\\';
    
    // '' <summary>
    // '' A string that contains all the special escape characters.
    // '' The order of these characters matches the order of the escapeText.
    // '' </summary>
    public static string escapeCharacters = "nt{}\\";
    
    public static string[] escapeText;
    
    // '' <summary>
    // '' A string containing Selector split characters.
    // '' Any of these characters chain together properties.
    // '' For example, {Person.Address.City}.
    // '' 
    // '' Two things to notice:
    // '' Multiple splitters in a row are ignored;
    // '' Parenthesis/brackets do NOT have to be matched, and they do NOT affect order of operations.
    // '' 
    // '' Therefore, the following examples are identical:
    // ''  {Person.Address.City} 
    // ''  {Person)Address]City}
    // ''  {[Person]Address(City)}
    // ''  {..Person...Address...City..}
    // ''  {..Person(Address)]]]City)[)..}
    // '' 
    // '' This allows comfortable syntaxes such as {Array[0].Item}
    // '' This syntax is identical to {Array.0.Item} and {Array]0]Item} and {[Array][0](Item)}
    // '' 
    // '' </summary>
    public static string selectorSplitters = ".[]()";
    
    public static string selectorCharacters = "_";
    
    // '' <summary>
    // '' Formats the format string using the parameters provided.
    // '' </summary>
    // '' <param name="format"></param>
    // '' <param name="args"></param>
    public static string CustomFormat(string format, params object[] args) {
        StringWriter output = new StringWriter(new StringBuilder((format.Length * 2)));
        //  Guessing a length can help performance a little.
        CustomFormat(new CustomFormatInfo(output, format, args));
        return output.ToString();
    }
    
    // '' <summary>
    // '' Performs the CustomFormat and outputs to a Stream.
    // '' </summary>
    public static void CustomFormat(Stream output, string format, params object[] args) {
        CustomFormat(new CustomFormatInfo(new StreamWriter(output), format, args));
    }
    
    // '' <summary>
    // '' Performs the CustomFormat and outputs to a TextWriter.
    // '' </summary>
    // '' <param name="output">Common types of TextWriters are StringWriter and StreamWriter.</param>
    public static void CustomFormat(TextWriter output, string format, params object[] args) {
        CustomFormat(new CustomFormatInfo(output, format, args));
    }
    
    // '' <summary>
    // '' Does the actual work.
    // '' </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void CustomFormat(CustomFormatInfo info) {
        if (((info.Current == null) 
                    && (info.Arguments.Length >= 1))) {
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
        while (NextPlaceholder(format, lastAppendedIndex, format.Length, placeholder)) {
            //  Write the text in-between placeholders:
            info.WriteRegularText(format, lastAppendedIndex, (placeholder.placeholderStart - lastAppendedIndex));
            lastAppendedIndex = (placeholder.placeholderStart + placeholder.placeholderLength);
            //  Evaluate the source by evaluating each argSelector:
            info.Current = current;
            //  Restore the current scope
            bool isFirstSelector = true;
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
                isFirstSelector = false;
            }
            //  Handle errors:
            if (!info.Handled) {
                //  If the ExtendCustomSource event wasn't handled,
                //  then the Selector could not be evaluated.
                if (!OnInvalidSelector(format, info, placeholder)) {
                    // TODO: Continue Do... Warning!!! not translated
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
            // '' <summary>
            // '' Returns True if the placeholder was formatted correctly; False if a placeholder couldn't be found.
            // '' Outputs all relevant placeholder information.
            // '' 
            // '' This function takes the place of the Regular Expression.
            // '' It is faster and more direct, and does not suffer from Regex endless loops.
            // '' In tests, this nearly doubles the speed vs Regex.
            // '' </summary>
            ((bool)(NextPlaceholder(format, startIndex, Percent, endIndex, Percent, ref ((PlaceholderInfo)(placeholder)))));
            placeholder = new PlaceholderInfo();
            placeholder.hasNested = false;
            placeholder.placeholderStart = -1;
            placeholder.selectorLength = -1;
            IList<string> selectorSplitList = new List<string>();
            object lastSplitIndex;
            int openCount = 0;
            // Dim endIndex% = format.Length
            while ((startIndex < endIndex)) {
                char c = format[startIndex];
                if ((placeholder.placeholderStart == -1)) {
                    //  Looking for "{"
                    if ((c == '{')) {
                        placeholder.placeholderStart = startIndex;
                        placeholder.selectorStart = (startIndex + 1);
                        lastSplitIndex = placeholder.selectorStart;
                    }
                    else if ((c == escapeCharacter)) {
                        //  The next character is escaped
                        startIndex++;
                    }
                }
                else if ((placeholder.selectorLength == -1)) {
                    //  Looking for ":" or "}" ...
                    //  or an alpha-numeric or a selectorSplitter
                    if ((c == '}')) {
                        //  Add this item to the list of Selectors (as long as it isn't empty)
                        if ((lastSplitIndex < startIndex)) {
                            selectorSplitList.Add(format.Substring(lastSplitIndex, (startIndex - lastSplitIndex)));
                        }
                        lastSplitIndex = (startIndex + 1);
                        placeholder.selectors = selectorSplitList.ToArray;
                        placeholder.placeholderLength = (startIndex + (1 - placeholder.placeholderStart));
                        placeholder.selectorLength = (startIndex - placeholder.selectorStart);
                        placeholder.formatLength = 0;
                        return true;
                    }
                    else if ((c == ":")) {
                        c;
                        //  Add this item to the list of Selectors (as long as it isn't empty)
                        if ((lastSplitIndex < startIndex)) {
                            selectorSplitList.Add(format.Substring(lastSplitIndex, (startIndex - lastSplitIndex)));
                        }
                        lastSplitIndex = (startIndex + 1);
                        placeholder.selectors = selectorSplitList.ToArray;
                        placeholder.selectorLength = (startIndex - placeholder.selectorStart);
                        placeholder.formatStart = (startIndex + 1);
                    }
                    else if ((selectorSplitters.IndexOf(c) >= 0)) {
                        //  It is a valid splitter character
                        //  Add this item to the list of Selectors (as long as it isn't empty)
                        if ((lastSplitIndex < startIndex)) {
                            selectorSplitList.Add(format.Substring(lastSplitIndex, (startIndex - lastSplitIndex)));
                        }
                        lastSplitIndex = (startIndex + 1);
                    }
                    else if ((char.IsLetterOrDigit(c) || selectorCharacters.Contains(c))) {
                        //  c = "_"c Then
                        //  It is a valid selector character, so let's just continue
                    }
                    else {
                        //  It is NOT a valid character!!!
                        if ((placeholder.selectorStart <= startIndex)) {
                            startIndex--;
                        }
                        placeholder.placeholderStart = -1;
                        //  Restart the search
                        selectorSplitList.Clear();
                    }
                }
                else {
                    //  We are in the Format section:
                    //  Looking for a "}"
                    if ((c == '}')) {
                        if ((openCount == 0)) {
                            // we're done!
                            placeholder.placeholderLength = (startIndex + (1 - placeholder.placeholderStart));
                            placeholder.formatLength = (startIndex - placeholder.formatStart);
                            return true;
                        }
                        else {
                            openCount--;
                        }
                    }
                    else if ((c == '{')) {
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
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            OnExtendCustomSource(((CustomSourceInfo)(info)));
            //  Fire the plugins event:
            ExtendCustomSource(info);
            ExtendCustomSourceDelegate(((CustomSourceInfo)(info)));
            ((Generic.SortedDictionary[])(CustomSourceHandlers));
            Of;
            CustomFormatPriorities;
            Generic.List(Of, ExtendCustomSourceDelegate);
            // '' <summary>
            // '' An event that allows custom Source to occur.
            // '' 
            // '' Why is it Custom?  2 reasons:
            // '' " This event short-circuits if a handler sets the output, to improve efficiency and redundancy
            // '' " This event allows you to set an "event priority" by applying the CustomFormatPriorityAttribute to the handler method!  (See "How to add your own Custom Formatter:" below)
            // '' 
            // '' This adds a little complexity to the event, but there are occassions when we need the CustomSource handlers to execute in a certain order.
            // '' </summary>
            Custom;
            ((ExtendCustomSourceDelegate)(value));
            if ((CustomSourceHandlers == null)) {
                //  Initialize the event dictionary:
                CustomSourceHandlers = new Generic.SortedDictionary(Of, CustomFormatPriorities, Generic.List(Of, ExtendCustomSourceDelegate));
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
                CustomSourceHandlers.Add(handlerPriority, new Generic.List(Of, ExtendCustomSourceDelegate));
            }
            //  Add the new handler to the list:
            CustomSourceHandlers(handlerPriority).Add(value);
             += // TODO: Warning!!!! NULL EXPRESSION DETECTED...
            ;
            ((ExtendCustomSourceDelegate)(value));
            if (CustomSourceHandlers) {
                IsNot;
                null;
                foreach (Generic.List[] lst in Of) {
                    ExtendCustomSourceDelegate;
                    CustomSourceHandlers.Values;
                    //  Search each list for the delegate
                    if (lst.Remove(value)) {
                        return;
                    }
                }
            }
            ((CustomSourceInfo)(info));
            if (CustomSourceHandlers) {
                IsNot;
                null;
                foreach (Generic.List[] lst in Of) {
                    ExtendCustomSourceDelegate;
                    CustomSourceHandlers.Values;
                    //  Go through each priority
                    foreach (ExtendCustomSourceDelegate handler in lst) {
                        //  Go through each list
                        if (info.Handled) {
                            return handler.Invoke(info);
                        }
                    }
                }
            }
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            OnExtendCustomFormat(((CustomFormatInfo)(info)));
            //  Fire the plugins event:
            ExtendCustomFormat(info);
            ExtendCustomFormatDelegate(((CustomFormatInfo)(info)));
            ((Generic.SortedDictionary[])(CustomFormatHandlers));
            Of;
            CustomFormatPriorities;
            Generic.List(Of, ExtendCustomFormatDelegate);
            // '' <summary>
            // '' An event that allows custom formatting to occur.
            // '' 
            // '' Why is it Custom?  2 reasons:
            // '' " This event short-circuits if a handler sets the output, to improve efficiency and redundancy
            // '' " This event allows you to set an "event priority" by applying the CustomFormatPriorityAttribute to the handler method!  (See "How to add your own Custom Formatter:" below)
            // '' 
            // '' This adds a little complexity to the event, but there are occassions when we need the CustomFormat handlers to execute in a certain order.
            // '' </summary>
            Custom;
            ((ExtendCustomFormatDelegate)(value));
            if ((CustomFormatHandlers == null)) {
                //  Initialize the event dictionary:
                CustomFormatHandlers = new Generic.SortedDictionary(Of, CustomFormatPriorities, Generic.List(Of, ExtendCustomFormatDelegate));
            }
            //  Let's search for the "CustomFormatPriorityAttribute" to see if we should add this handler at a higher priority in the handler list:
            CustomFormatPriorities handlerPriority = CustomFormatPriorities.Normal;
            //  default priority
            foreach (CustomFormatPriorityAttribute pa in value.Method.GetCustomAttributes(typeof(CustomFormatPriorityAttribute), true)) {
                handlerPriority = pa.Priority;
                //  There should never be more than 1 PriorityAttribute
            }
            //  Make sure there is a list for this priority:
            if (!CustomFormatHandlers.ContainsKey(handlerPriority)) {
                CustomFormatHandlers.Add(handlerPriority, new Generic.List(Of, ExtendCustomFormatDelegate));
            }
            //  Add the new handler to the list:
            CustomFormatHandlers(handlerPriority).Add(value);
             += // TODO: Warning!!!! NULL EXPRESSION DETECTED...
            ;
            ((ExtendCustomFormatDelegate)(value));
            if (CustomFormatHandlers) {
                IsNot;
                null;
                foreach (Generic.List[] lst in Of) {
                    ExtendCustomFormatDelegate;
                    CustomFormatHandlers.Values;
                    //  Search each list for the delegate
                    if (lst.Remove(value)) {
                        return;
                    }
                }
            }
            ((CustomFormatInfo)(info));
            if (CustomFormatHandlers) {
                IsNot;
                null;
                foreach (Generic.List[] lst in Of) {
                    ExtendCustomFormatDelegate;
                    CustomFormatHandlers.Values;
                    //  Go through each priority
                    foreach (ExtendCustomFormatDelegate handler in lst) {
                        //  Go through each list
                        if (info.Handled) {
                            return handler.Invoke(info);
                        }
                    }
                }
            }
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            // '' <summary>
            // '' This is the Default method for evaluating the Source.
            // '' 
            // '' 
            // '' If this is the first selector and the selector is an integer, then it returns the (global) indexed argument (just like String.Format).
            // '' If the Current item is a Dictionary that contains the Selector, it returns the dictionary item.
            // '' Otherwise, Reflection will be used to determine if the Selector is a Property, Field, or Method of the Current item.
            // '' </summary>
            _GetDefaultSource(((CustomSourceInfo)(info)));
            ExtendCustomSource;
            //  If it wasn't handled, let's evaluate the source on our own:
            //  We will see if it's an argument index, dictionary key, or a property/field/method.
            //  Maybe source is the global index of our arguments? 
            object argIndex;
            if (((info.SelectorIndex == 0) 
                        && int.TryParse(info.Selector, argIndex))) {
                if ((argIndex < info.Arguments.Length)) {
                    info.Current = info.Arguments[argIndex];
                }
                else {
                    //  The index is out-of-range!
                }
                return;
            }
            //  Maybe source is a Dictionary?
            info.Current;
            IDictionary;
            Contains(info.Selector);
            info.Current;
            IDictionary;
            info.Selector;
            return;
            ((Type)(sourceType)) = info.Current.GetType;
            MemberInfo[] members = sourceType.GetMember(info.Selector);
            foreach (MemberInfo member in members) {
                switch (member.MemberType) {
                    case MemberTypes.Field:
                        //  Selector is a Field; retrieve the value:
                        FieldInfo field = member;
                        info.Current = field.GetValue(info.Current);
                        return;
                        break;
                    case MemberTypes.Property:
                    case MemberTypes.Method:
                        MethodInfo method;
                        if ((member.MemberType == MemberTypes.Property)) {
                            //  Selector is a Property
                            PropertyInfo prop = member;
                            //  Make sure the property is not WriteOnly:
                            if (prop.CanRead) {
                                method = prop.GetGetMethod;
                            }
                            else {
                                // TODO: Continue For... Warning!!! not translated
                            }
                        }
                        else {
                            //  Selector is a Method
                            method = member;
                        }
                        //  Check that this method is valid -- it needs to be a Function (return a value) and has to be parameterless:
                        //  We are only looking for a parameterless Property/Method:
                        if ((method.GetParameters.Length > 0)) {
                            // TODO: Continue For... Warning!!! not translated
                        }
                        //  Make sure that this method is not a Sub!  It has to be a Function!
                        if ((method.ReturnType == typeof(Void))) {
                            // TODO: Continue For... Warning!!! not translated
                        }
                        //  Retrieve the Property/Method value:
                        info.Current = method.Invoke(info.Current, new object[0]);
                        return;
                        break;
                }
            }
            //  If we haven't returned yet, then the item must be invalid.
            // '' <summary>
            // '' This is the Default method for formatting the output.
            // '' This code has been derived from the built-in String.Format() function.
            // '' </summary>
            _GetDefaultOutput(((CustomFormatInfo)(info)));
            ExtendCustomFormat;
            //  Let's see if there are nested items:
            if (info.HasNested) {
                info.CustomFormatNested();
                return;
            }
            //  Let's do the default formatting:
            //  We will try using IFormatProvider, IFormattable, and if all else fails, ToString.
            //  (This code was adapted from the built-in String.Format code)
            if (info.Provider) {
                IsNot;
                null;
                //  Use the provider to see if a CustomFormatter is available:
                ICustomFormatter formatter = info.Provider.GetFormat(typeof(ICustomFormatter));
                if (formatter) {
                    IsNot;
                    null;
                    info.Write(formatter.Format(info.Format, info.Current, info.Provider));
                    return;
                }
                //  Now try to format the object, using its own built-in formatting if possible:
                if ((info.Current.GetType() == IFormattable)) {
                    info.Write(DirectCast, info.Current, IFormattable).ToString(info.Format, info.Provider);
                }
                else {
                    info.Write(info.Current.ToString);
                }
            }
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            //  Makes it easier to spot errors while debugging.
            // TODO: #If Then ... Warning!!! not translated
            // '' <summary>
            // '' Determines what to do if a Format string cannot be successfully evaluated.
            // '' </summary>
            ((ErrorActions)(InvalidSelectorAction)) = ErrorActions.ThrowError;
            ((ErrorActions)(InvalidFormatAction)) = ErrorActions.ThrowError;
            // TODO: # ... Warning!!! not translated
            // '' <summary>
            // '' Determines what to do if a Format string cannot be successfully evaluated.
            // '' </summary>
            ((ErrorActions)(InvalidSelectorAction)) = ErrorActions.OutputErrorInResult;
            ((ErrorActions)(InvalidFormatAction)) = ErrorActions.OutputErrorInResult;
            // TODO: #End If ... Warning!!! not translated
            Enum;
            ErrorActions;
            // '' <summary>Throws an exception.  This is only recommended for debugging, so that formatting errors can be easily found.</summary>
            ThrowError;
            // '' <summary>Includes an error message in the output</summary>
            OutputErrorInResult;
            // '' <summary>Ignores errors and tries to output the data anyway</summary>
            Ignore;
            Enum;
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            // '' <summary>
            // '' Determines what to do when an Invalid Selector is found.
            // '' 
            // '' Returns True if we should just continue; False if we should skip this item.
            // '' </summary>
            ((bool)(OnInvalidSelector(format, ((CustomFormatInfo)(info)), ((PlaceholderInfo)(placeholder)))));
            object invalidSelector = format.Substring(placeholder.selectorStart, placeholder.selectorLength);
            switch (InvalidSelectorAction) {
                case ErrorActions.ThrowError:
                    //  Let's give a detailed description of the error:
                    object message = CustomFormat(("Invalid Format String.\\n" + ("Could not evaluate \"{0}\": \"{1}\" is not a member of {2}.\\n" + ("The error occurs at position {3} of the following format string:\\n" + "{4}"))), invalidSelector, info.Selector, info.CurrentType, placeholder.placeholderStart, format);
                    throw new ArgumentException(message, invalidSelector);
                    break;
                case ErrorActions.OutputErrorInResult:
                    //  Let's put the placeholder back,
                    //  along with the error.
                    //  Example: {Person.Name.ABC}  becomes  {Person.Name.ABC:(Error: "ABC" is not a member of String)}
                    object message = ("{" 
                                + (CustomFormat("{0}:(Error: \"{1}\" is not a member of {2})", invalidSelector, info.Selector, info.CurrentType) + "}"));
                    info.WriteError(message, placeholder);
                    return false;
                    break;
                case ErrorActions.Ignore:
                    //  Allow formatting to continue!
                    break;
            }
            return true;
            // '' <summary>
            // '' Determines what to do when an Invalid Selector is found.
            // '' </summary>
            OnInvalidFormat(format, ((CustomFormatInfo)(info)), ((PlaceholderInfo)(placeholder)), ((Exception)(ex)));
            object selector = format.Substring(placeholder.selectorStart, placeholder.selectorLength);
            object invalidFormat = format.Substring(placeholder.formatStart, placeholder.formatLength);
            object errorMessage = ex.Message;
            if ((ex.GetType() == FormatException)) {
                errorMessage = CustomFormat("\"{0}\" is not a valid format specifier for {1}", invalidFormat, info.CurrentType);
            }
            switch (InvalidFormatAction) {
                case ErrorActions.ThrowError:
                    //  Let's give a detailed description of the error:
                    object message = CustomFormat(("Invalid Format String.\\n" + ("Could not evaluate {{0}} because {1}.\\n" + ("The error occurs at position {2} of the following format string:\\n" + "{3}"))), selector, errorMessage, placeholder.placeholderStart, format);
                    throw new ArgumentException(message, invalidFormat, ex);
                    break;
                case ErrorActions.OutputErrorInResult:
                    //  Let's put the placeholder back,
                    //  along with the error.
                    //  Example: {Person.Birthday:x}  becomes  {Person.Birthday:(Error: "x" is an invalid format specifier)}
                    object message = ("{" 
                                + (CustomFormat("{0}:(Error: {1})", selector, errorMessage) + "}"));
                    info.WriteError(message, placeholder);
                    break;
                case ErrorActions.Ignore:
                    //  Allow formatting to continue!
                    break;
            }
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            // TODO: #End Region ... Warning!!! not translated
            // TODO: #Region ... Warning!!! not translated
            // '' <summary>
            // '' Enhances String.Split by ignoring any characters that are between the nested characters.
            // '' It also allows you to stop after a certain number of splits.
            // '' 
            // '' Example:
            // '' SplitNested("a|b{1|2|3}|c", "|"c) = {"a", "b{1|2|3}", "c"}
            // '' SplitNested("a|b{1|2|3}|c", "|"c, 2) = {"a", "b{1|2|3}|c"}
            // '' 
            // '' </summary>
            ((string[])(SplitNested(format, ((char)(splitChar)), Optional, maxItemsAsInteger=0)));
            object openCount;
            0;
            Generic.List items = new Generic.List(Of, String);
            4;
            //  (Estimating 4 matches)
            object lastSplit;
            0;
            for (int i = 0; (i 
                        <= (format.Length - 1)); i++) {
                char c = format[i];
                if ((c == "{")) {
                    c;
                    openCount++;
                }
                else if ((c == "}")) {
                    c;
                    openCount--;
                }
                else if (((c == splitChar) 
                            && (openCount == 0))) {
                    items.Add(format.Substring(lastSplit, (i - lastSplit)));
                    lastSplit = (i + 1);
                    if (((maxItems != 0) 
                                && (items.Count 
                                == (maxItems - 1)))) {
                        break;
                    }
                    items.Add(format.Substring(lastSplit));
                    return items.ToArray;
                }
                // TODO: #End Region ... Warning!!! not translated
            }
        }
    }
}