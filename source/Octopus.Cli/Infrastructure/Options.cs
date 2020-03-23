//
// Options.cs
//
// Authors:
//  Jonathan Pryor <jpryor@novell.com>
//
// Copyright (C) 2008 Novell (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Compile With:
//   gmcs -debug+ -r:System.Core Options.cs -o:NDesk.Options.dll
//   gmcs -debug+ -d:LINQ -r:System.Core Options.cs -o:NDesk.Options.dll
//
// The LINQ version just changes the implementation of
// OptionSet.Parse(IEnumerable<string>), and confers no semantic changes.

//
// A Getopt::Long-inspired option parsing library for C#.
//
// NDesk.Options.OptionSet is built upon a key/value table, where the
// key is a option format string and the value is a delegate that is 
// invoked when the format string is matched.
//
// Option format strings:
//  Regex-like BNF Grammar: 
//    name: .+
//    type: [=:]
//    sep: ( [^{}]+ | '{' .+ '}' )?
//    aliases: ( name type sep ) ( '|' name type sep )*
// 
// Each '|'-delimited name is an alias for the associated action.  If the
// format string ends in a '=', it has a required value.  If the format
// string ends in a ':', it has an optional value.  If neither '=' or ':'
// is present, no value is supported.  `=' or `:' need only be defined on one
// alias, but if they are provided on more than one they must be consistent.
//
// Each alias portion may also end with a "key/value separator", which is used
// to split option values if the option accepts > 1 value.  If not specified,
// it defaults to '=' and ':'.  If specified, it can be any character except
// '{' and '}' OR the *string* between '{' and '}'.  If no separator should be
// used (i.e. the separate values should be distinct arguments), then "{}"
// should be used as the separator.
//
// Options are extracted either from the current option by looking for
// the option name followed by an '=' or ':', or is taken from the
// following option IFF:
//  - The current option does not contain a '=' or a ':'
//  - The current option requires a value (i.e. not a Option type of ':')
//
// The `name' used in the option format string does NOT include any leading
// option indicator, such as '-', '--', or '/'.  All three of these are
// permitted/required on any named option.
//
// Option bundling is permitted so long as:
//   - '-' is used to start the option group
//   - all of the bundled options are a single character
//   - at most one of the bundled options accepts a value, and the value
//     provided starts from the next character to the end of the string.
//
// This allows specifying '-a -b -c' as '-abc', and specifying '-D name=value'
// as '-Dname=value'.
//
// Option processing is disabled by specifying "--".  All options after "--"
// are returned by OptionSet.Parse() unchanged and unprocessed.
//
// Unprocessed options are returned from OptionSet.Parse().
//
// Examples:
//  int verbose = 0;
//  OptionSet p = new OptionSet ()
//    .Add ("v", v => ++verbose)
//    .Add ("name=|value=", v => Console.WriteLine (v));
//  p.Parse (new string[]{"-v", "--v", "/v", "-name=A", "/name", "B", "extra"});
//
// The above would parse the argument string array, and would invoke the
// lambda expression three times, setting `verbose' to 3 when complete.  
// It would also print out "A" and "B" to standard output.
// The returned array would contain the string "extra".
//
// C# 3.0 collection initializers are supported and encouraged:
//  var p = new OptionSet () {
//    { "h|?|help", v => ShowHelp () },
//  };
//
// System.ComponentModel.TypeConverter is also supported, allowing the use of
// custom data types in the callback type; TypeConverter.ConvertFromString()
// is used to convert the value option to an instance of the specified
// type:
//
//  var p = new OptionSet () {
//    { "foo=", (Foo f) => Console.WriteLine (f.ToString ()) },
//  };
//
// Random other tidbits:
//  - Boolean options (those w/o '=' or ':' in the option format string)
//    are explicitly enabled if they are followed with '+', and explicitly
//    disabled if they are followed with '-':
//      string a = null;
//      var p = new OptionSet () {
//        { "a", s => a = s },
//      };
//      p.Parse (new string[]{"-a"});   // sets v != null
//      p.Parse (new string[]{"-a+"});  // sets v != null
//      p.Parse (new string[]{"-a-"});  // sets v == null
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Octopus.Cli.Util;

namespace Octopus.Cli.Infrastructure
{
    public class OptionValueCollection : IList, IList<string>
    {
        readonly OptionContext c;
        readonly List<string> values = new List<string>();

        internal OptionValueCollection(OptionContext c)
        {
            this.c = c;
        }

        #region ICollection

        void ICollection.CopyTo(Array array, int index)
        {
            (values as ICollection).CopyTo(array, index);
        }

        bool ICollection.IsSynchronized => (values as ICollection).IsSynchronized;

        object ICollection.SyncRoot => (values as ICollection).SyncRoot;

        #endregion

        #region ICollection<T>

        public void Clear()
        {
            values.Clear();
        }

        public int Count => values.Count;

        public bool IsReadOnly => false;

        public void Add(string item)
        {
            values.Add(item);
        }

        public bool Contains(string item)
        {
            return values.Contains(item);
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            values.CopyTo(array, arrayIndex);
        }

        public bool Remove(string item)
        {
            return values.Remove(item);
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator()
        {
            return values.GetEnumerator();
        }

        #endregion

        #region IEnumerable<T>

        public IEnumerator<string> GetEnumerator()
        {
            return values.GetEnumerator();
        }

        #endregion

        #region IList

        int IList.Add(object value)
        {
            return (values as IList).Add(value);
        }

        bool IList.Contains(object value)
        {
            return (values as IList).Contains(value);
        }

        int IList.IndexOf(object value)
        {
            return (values as IList).IndexOf(value);
        }

        void IList.Insert(int index, object value)
        {
            (values as IList).Insert(index, value);
        }

        void IList.Remove(object value)
        {
            (values as IList).Remove(value);
        }

        void IList.RemoveAt(int index)
        {
            (values as IList).RemoveAt(index);
        }

        bool IList.IsFixedSize => false;

        object IList.this[int index]
        {
            get { return this[index]; }
            set { (values as IList)[index] = value; }
        }

        #endregion

        #region IList<T>

        public int IndexOf(string item)
        {
            return values.IndexOf(item);
        }

        public void Insert(int index, string item)
        {
            values.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            values.RemoveAt(index);
        }

        public string this[int index]
        {
            get
            {
                AssertValid(index);
                return index >= values.Count ? null : values[index];
            }
            set { values[index] = value; }
        }

        void AssertValid(int index)
        {
            if (c.Option == null)
                throw new InvalidOperationException("OptionContext.Option is null.");
            if (index >= c.Option.MaxValueCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (c.Option.OptionValueType == OptionValueType.Required &&
                index >= values.Count)
                throw new CommandException($"Missing required value for option '{c.OptionName}'.");
        }

        #endregion

        public List<string> ToList()
        {
            return new List<string>(values);
        }

        public string[] ToArray()
        {
            return values.ToArray();
        }

        public override string ToString()
        {
            return string.Join(", ", values.ToArray());
        }
    }

    public class OptionContext
    {
        public OptionContext(OptionSet set)
        {
            this.OptionSet = set;
            OptionValues = new OptionValueCollection(this);
        }

        public Option Option { get; set; }

        public string OptionName { get; set; }

        public int OptionIndex { get; set; }

        public OptionSet OptionSet { get; }

        public OptionValueCollection OptionValues { get; }
    }

    public enum OptionValueType
    {
        None,
        Optional,
        Required,
    }

    public abstract class Option
    {
        static readonly char[] NameTerminator = {'=', ':'};

        protected Option(string prototype, string description, bool sensitive = false)
            : this(prototype, description, 1, sensitive)
        {
        }

        protected Option(string prototype, string description, int maxValueCount, bool sensitive = false)
        {
            if (prototype == null)
                throw new ArgumentNullException(nameof(prototype));
            if (prototype.Length == 0)
                throw new ArgumentException("Cannot be the empty string.", nameof(prototype));
            if (maxValueCount < 0)
                throw new ArgumentOutOfRangeException(nameof(maxValueCount));

            this.Prototype = prototype;
            Names = prototype.Split('|');
            this.Description = description;
            MaxValueCount = maxValueCount;
            Sensitive = sensitive;
            OptionValueType = ParsePrototype();

            if (MaxValueCount == 0 && OptionValueType != OptionValueType.None)
                throw new ArgumentException(
                    "Cannot provide maxValueCount of 0 for OptionValueType.Required or " +
                    "OptionValueType.Optional.",
                    nameof(maxValueCount));
            if (OptionValueType == OptionValueType.None && maxValueCount > 1)
                throw new ArgumentException(
                    string.Format("Cannot provide maxValueCount of {0} for OptionValueType.None.", maxValueCount),
                    nameof(maxValueCount));
            if (Array.IndexOf(Names, "<>") >= 0 &&
                ((Names.Length == 1 && OptionValueType != OptionValueType.None) ||
                 (Names.Length > 1 && MaxValueCount > 1)))
                throw new ArgumentException(
                    "The default option handler '<>' cannot require values.",
                    nameof(prototype));
        }

        public string Prototype { get; }

        public string Description { get; }

        public OptionValueType OptionValueType { get; }

        public int MaxValueCount { get; }
        public bool Sensitive { get; }

        internal string[] Names { get; }

        internal string[] ValueSeparators { get; private set; }
        public abstract Type Type { get; }

        public string[] GetNames()
        {
            return (string[]) Names.Clone();
        }

        public string[] GetValueSeparators()
        {
            if (ValueSeparators == null)
                return new string[0];
            return (string[]) ValueSeparators.Clone();
        }

        protected static T Parse<T>(string value, OptionContext c)
        {
            var conv = TypeDescriptor.GetConverter(typeof (T));
            var t = default(T);
            try
            {
                if (value != null)
                    t = (T) conv.ConvertFromString(value);
            }
            catch(FormatException) when(typeof(T).IsEnum)
            {
                throw new CommandException($"Could not convert string `{value}' to type {typeof(T).Name} for option `{c.OptionName}'. Valid values are {Enum.GetNames(typeof(T)).ReadableJoin()}.");
            }
            catch(Exception ex) when(!(ex is CommandException))
            {
                throw new CommandException($"Could not convert string `{value}' to type {typeof(T).Name} for option `{c.OptionName}'.");
            }
            return t;
        }

        OptionValueType ParsePrototype()
        {
            var c = '\0';
            var seps = new List<string>();
            for (var i = 0; i < Names.Length; ++i)
            {
                var name = Names[i];
                if (name.Length == 0)
                    throw new ArgumentException("Empty option names are not supported.");

                var end = name.IndexOfAny(NameTerminator);
                if (end == -1)
                    continue;
                Names[i] = name.Substring(0, end);
                if (c == '\0' || c == name[end])
                    c = name[end];
                else
                    throw new ArgumentException(
                        string.Format("Conflicting option types: '{0}' vs. '{1}'.", c, name[end]));
                AddSeparators(name, end, seps);
            }

            if (c == '\0')
                return OptionValueType.None;

            if (MaxValueCount <= 1 && seps.Count != 0)
                throw new ArgumentException(
                    string.Format("Cannot provide key/value separators for Options taking {0} value(s).", MaxValueCount));
            if (MaxValueCount > 1)
            {
                if (seps.Count == 0)
                    ValueSeparators = new[] {":", "="};
                else if (seps.Count == 1 && seps[0].Length == 0)
                    ValueSeparators = null;
                else
                    ValueSeparators = seps.ToArray();
            }

            return c == '=' ? OptionValueType.Required : OptionValueType.Optional;
        }

        static void AddSeparators(string name, int end, ICollection<string> seps)
        {
            var start = -1;
            for (var i = end + 1; i < name.Length; ++i)
            {
                switch (name[i])
                {
                    case '{':
                        if (start != -1)
                            throw new ArgumentException(
                                string.Format("Ill-formed name/value separator found in \"{0}\".", name));
                        start = i + 1;
                        break;
                    case '}':
                        if (start == -1)
                            throw new ArgumentException(
                                string.Format("Ill-formed name/value separator found in \"{0}\".", name));
                        seps.Add(name.Substring(start, i - start));
                        start = -1;
                        break;
                    default:
                        if (start == -1)
                            seps.Add(name[i].ToString());
                        break;
                }
            }
            if (start != -1)
                throw new ArgumentException(
                    string.Format("Ill-formed name/value separator found in \"{0}\".", name));
        }

        public void Invoke(OptionContext c)
        {
            OnParseComplete(c);
            c.OptionName = null;
            c.Option = null;
            c.OptionValues.Clear();
        }

        protected abstract void OnParseComplete(OptionContext c);

        public override string ToString()
        {
            return Prototype;
        }
    }

    public class OptionException : Exception
    {
        public OptionException()
        {
        }

        public OptionException(string message, string optionName)
            : base(message)
        {
            OptionName = optionName;
        }

        public OptionException(string message, string optionName, Exception innerException)
            : base(message, innerException)
        {
            OptionName = optionName;
        }

        public string OptionName { get; }
    }

    public delegate void OptionAction<in TKey, in TValue>(TKey key, TValue value);

    public class OptionSet : KeyedCollection<string, Option>
    {
        Action<string[]> leftovers;

        public OptionSet()
        {
        }


        protected override string GetKeyForItem(Option item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (item.Names != null && item.Names.Length > 0)
                return item.Names[0];
            // This should never happen, as it's invalid for Option to be
            // constructed w/o any names.
            throw new InvalidOperationException("Option has no names!");
        }

        [Obsolete("Use KeyedCollection.this[string]")]
        protected Option GetOptionForName(string option)
        {
            if (option == null)
                throw new ArgumentNullException(nameof(option));
            try
            {
                return base[option];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        protected override void InsertItem(int index, Option item)
        {
            base.InsertItem(index, item);
            AddImpl(item);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            var p = Items[index];
            // KeyedCollection.RemoveItem() handles the 0th item
            for (var i = 1; i < p.Names.Length; ++i)
            {
                Dictionary.Remove(p.Names[i]);
            }
        }

        protected override void SetItem(int index, Option item)
        {
            base.SetItem(index, item);
            RemoveItem(index);
            AddImpl(item);
        }

        void AddImpl(Option option)
        {
            if (option == null)
                throw new ArgumentNullException(nameof(option));
            var added = new List<string>(option.Names.Length);
            try
            {
                // KeyedCollection.InsertItem/SetItem handle the 0th name.
                for (var i = 1; i < option.Names.Length; ++i)
                {
                    Dictionary.Add(option.Names[i], option);
                    added.Add(option.Names[i]);
                }
            }
            catch (Exception)
            {
                foreach (var name in added)
                    Dictionary.Remove(name);
                throw;
            }
        }

        public new OptionSet Add(Option option)
        {
            base.Add(option);
            return this;
        }

        sealed class ActionOption : Option
        {
            readonly Action<OptionValueCollection> action;

            public ActionOption(string prototype, string description, int count, Action<OptionValueCollection> action)
                : base(prototype, description, count)
            {
                if (action == null)
                    throw new ArgumentNullException(nameof(action));
                this.action = action;
            }

            public override Type Type => typeof(string);

            protected override void OnParseComplete(OptionContext c)
            {
                action(c.OptionValues);
            }
        }

        sealed class ActionOption<T> : Option
        {
            readonly Action<T> action;

            public ActionOption(string prototype, string description, Action<T> action, bool sensitive)
                : base(prototype, description, 1, sensitive)
            {
                if (action == null)
                    throw new ArgumentNullException(nameof(action));
                this.action = action;
            }

            public override Type Type => typeof(T);

            protected override void OnParseComplete(OptionContext c)
            {
                if (this.OptionValueType == OptionValueType.None)
                    action(default(T));
                else
                    action(Parse<T>(c.OptionValues[0], c));
            }
        }

        public OptionSet Add<T>(string prototype, Action<T> action)
        {
            return Add(prototype, null, action);
        }

        public OptionSet Add<T>(string prototype, string description, Action<T> action, bool sensitive = false)
        {
            return Add(new ActionOption<T>(prototype, description, action, sensitive));
        }

        protected virtual OptionContext CreateOptionContext()
        {
            return new OptionContext(this);
        }

        public OptionSet WithExtras(Action<string[]> lo)
        {
            leftovers = lo;
            return this;
        }

        public List<string> Parse(IEnumerable<string> arguments)
        {
            var process = true;
            var optionContext = CreateOptionContext();
            optionContext.OptionIndex = -1;
#pragma warning disable 618
            var currentOption = GetOptionForName("<>");
#pragma warning restore 618
            var unprocessed = arguments.Where(argument => ParseOption(argument, optionContext, currentOption, ref process));
            var r = unprocessed.ToList();
            optionContext.Option?.Invoke(optionContext);

            if (leftovers != null && r.Count > 0)
            {
                leftovers(r.ToArray());
            }

            return r;
        }

        private bool ParseOption(string argument, OptionContext optionContext, Option currentOption, ref bool continueProcessing)
        {
            if (++optionContext.OptionIndex >= 0 && (continueProcessing || currentOption != null))
            {
                if (continueProcessing)
                {
                    if (argument == "--")
                        return continueProcessing = false;
                    if (!Parse(argument, optionContext))
                    {
                        if (currentOption != null)
                            return Unprocessed(null, currentOption, optionContext, argument);
                        return true;
                    }
                    return false;
                }

                if (currentOption != null)
                    return Unprocessed(null, currentOption, optionContext, argument);
                return true;
            }

            return true;
        }

        static bool Unprocessed(ICollection<string> extra, Option def, OptionContext c, string argument)
        {
            if (def == null)
            {
                extra.Add(argument);
                return false;
            }
            c.OptionValues.Add(argument);
            c.Option = def;
            c.Option.Invoke(c);
            return false;
        }

        readonly Regex ValueOption = new Regex(
            @"^(?<flag>--|-|/)(?<name>[^:=]+)((?<sep>[:=])(?<value>.*))?$");

#pragma warning disable 649
#pragma warning restore 649

        protected bool GetOptionParts(string argument, out string flag, out string name, out string sep, out string value)
        {
            if (argument == null)
                throw new ArgumentNullException(nameof(argument));

            flag = name = sep = value = null;
            var m = ValueOption.Match(argument);
            if (!m.Success)
            {
                return false;
            }
            flag = m.Groups["flag"].Value;
            name = m.Groups["name"].Value;
            if (m.Groups["sep"].Success && m.Groups["value"].Success)
            {
                sep = m.Groups["sep"].Value;
                value = m.Groups["value"].Value;
            }
            return true;
        }

        protected virtual bool Parse(string argument, OptionContext c)
        {
            if (c.Option != null)
            {
                ParseValue(argument, c);
                return true;
            }

            string f, n, s, v;
            if (!GetOptionParts(argument, out f, out n, out s, out v))
                return false;

            var p = this.FirstOrDefault(x => x.Names.Any(y => string.Equals(y, n, StringComparison.OrdinalIgnoreCase)));
            if (p != null)
            {
                c.OptionName = f + n;
                c.Option = p;
                switch (p.OptionValueType)
                {
                    case OptionValueType.None:
                        c.OptionValues.Add(n);
                        c.Option.Invoke(c);
                        break;
                    case OptionValueType.Optional:
                    case OptionValueType.Required:
                        ParseValue(v, c);
                        break;
                }
                return true;
            }
            // no match; is it a bool option?
            if (ParseBool(argument, n, c))
                return true;
            // is it a bundled option?
// ReSharper disable once PossiblyMistakenUseOfParamsMethod
            if (ParseBundledValue(f, string.Concat(n + s + v), c))
                return true;

            return false;
        }

        public bool ShouldWaitForExit { get; }

        void ParseValue(string option, OptionContext c)
        {
            if (option != null)
                foreach (var o in c.Option.ValueSeparators != null
                    ? option.Split(c.Option.ValueSeparators, StringSplitOptions.None)
                    : new[] {option})
                {
                    c.OptionValues.Add(o);
                }
            if (c.OptionValues.Count == c.Option.MaxValueCount ||
                c.Option.OptionValueType == OptionValueType.Optional)
                c.Option.Invoke(c);
            else if (c.OptionValues.Count > c.Option.MaxValueCount)
            {
                throw new OptionException($"Error: Found {c.OptionValues.Count} option values when expecting {c.Option.MaxValueCount}.", c.OptionName);
            }
        }

        bool ParseBool(string option, string n, OptionContext c)
        {
            string rn;
            if (n.Length >= 1 && (n[n.Length - 1] == '+' || n[n.Length - 1] == '-') &&
                Contains((rn = n.Substring(0, n.Length - 1))))
            {
                var p = this[rn];
                var v = n[n.Length - 1] == '+' ? option : null;
                c.OptionName = option;
                c.Option = p;
                c.OptionValues.Add(v);
                p.Invoke(c);
                return true;
            }
            return false;
        }

        bool ParseBundledValue(string f, string n, OptionContext c)
        {
            if (f != "-")
                return false;
            for (var i = 0; i < n.Length; ++i)
            {
                var opt = f + n[i];
                var rn = n[i].ToString();
                if (!Contains(rn))
                {
                    if (i == 0)
                        return false;
                    throw new OptionException($"Cannot bundle unregistered option '{opt}'.", opt);
                }
                var p = this[rn];
                switch (p.OptionValueType)
                {
                    case OptionValueType.None:
                        Invoke(c, opt, n, p);
                        break;
                    case OptionValueType.Optional:
                    case OptionValueType.Required:
                    {
                        var v = n.Substring(i + 1);
                        c.Option = p;
                        c.OptionName = opt;
                        ParseValue(v.Length != 0 ? v : null, c);
                        return true;
                    }
                    default:
                        throw new InvalidOperationException("Unknown OptionValueType: " + p.OptionValueType);
                }
            }
            return true;
        }

        static void Invoke(OptionContext c, string name, string value, Option option)
        {
            c.OptionName = name;
            c.Option = option;
            c.OptionValues.Add(value);
            option.Invoke(c);
        }

        const int OptionWidth = 29;

        public void WriteOptionDescriptions(TextWriter o)
        {
            foreach (var p in this)
            {
                var written = 0;
                if (!WriteOptionPrototype(o, p, ref written))
                    continue;

                if (written < OptionWidth)
                    o.Write(new string(' ', OptionWidth - written));
                else
                {
                    o.WriteLine();
                    o.Write(new string(' ', OptionWidth));
                }

                var lines = GetLines(GetDescription(p.Description));
                o.WriteLine(lines[0]);
                var prefix = new string(' ', OptionWidth);
                for (var i = 1; i < lines.Count; ++i)
                {
                    o.Write(prefix);
                    o.WriteLine(lines[i]);
                }
            }
        }

        bool WriteOptionPrototype(TextWriter o, Option p, ref int written)
        {
            var names = p.Names;

            var i = GetNextOptionIndex(names, 0);
            if (i == names.Length)
                return false;

            if (names[i].Length == 1)
            {
                Write(o, ref written, "  -");
                Write(o, ref written, names[0]);
            }
            else
            {
                Write(o, ref written, "      --");
                Write(o, ref written, names[0]);
            }

            for (i = GetNextOptionIndex(names, i + 1);
                i < names.Length;
                i = GetNextOptionIndex(names, i + 1))
            {
                Write(o, ref written, ", ");
                Write(o, ref written, names[i].Length == 1 ? "-" : "--");
                Write(o, ref written, names[i]);
            }

            if (p.OptionValueType == OptionValueType.Optional ||
                p.OptionValueType == OptionValueType.Required)
            {
                if (p.OptionValueType == OptionValueType.Optional)
                {
                    Write(o, ref written, "[");
                }
                Write(o, ref written, "=" + GetArgumentName(0, p.MaxValueCount, p.Description));
                var sep = p.ValueSeparators != null && p.ValueSeparators.Length > 0
                    ? p.ValueSeparators[0]
                    : " ";
                for (var c = 1; c < p.MaxValueCount; ++c)
                {
                    Write(o, ref written, sep + GetArgumentName(c, p.MaxValueCount, p.Description));
                }
                if (p.OptionValueType == OptionValueType.Optional)
                {
                    Write(o, ref written, "]");
                }
            }
            return true;
        }

        static int GetNextOptionIndex(string[] names, int i)
        {
            while (i < names.Length && names[i] == "<>")
            {
                ++i;
            }
            return i;
        }

        static void Write(TextWriter o, ref int n, string s)
        {
            n += s.Length;
            o.Write(s);
        }

        static string GetArgumentName(int index, int maxIndex, string description)
        {
            if (description == null)
                return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
            string[] nameStart;
            if (maxIndex == 1)
                nameStart = new[] {"{0:", "{"};
            else
                nameStart = new[] {"{" + index + ":"};
            for (var i = 0; i < nameStart.Length; ++i)
            {
                int start, j = 0;
                do
                {
                    start = description.IndexOf(nameStart[i], j, StringComparison.Ordinal);
                } while (start >= 0 && j != 0 && description[j++ - 1] == '{');
                if (start == -1)
                    continue;
                var end = description.IndexOf("}", start, StringComparison.Ordinal);
                if (end == -1)
                    continue;
                return description.Substring(start + nameStart[i].Length, end - start - nameStart[i].Length);
            }
            return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
        }

        static string GetDescription(string description)
        {
            if (description == null)
                return string.Empty;
            var sb = new StringBuilder(description.Length);
            var start = -1;
            for (var i = 0; i < description.Length; ++i)
            {
                switch (description[i])
                {
                    case '{':
                        if (i == start)
                        {
                            sb.Append('{');
                            start = -1;
                        }
                        else if (start < 0)
                            start = i + 1;
                        break;
                    case '}':
                        if (start < 0)
                        {
                            if ((i + 1) == description.Length || description[i + 1] != '}')
                                throw new InvalidOperationException("Invalid option description: " + description);
                            ++i;
                            sb.Append("}");
                        }
                        else
                        {
                            sb.Append(description.Substring(start, i - start));
                            start = -1;
                        }
                        break;
                    case ':':
                        if (start < 0)
                            goto default;
                        start = i + 1;
                        break;
                    default:
                        if (start < 0)
                            sb.Append(description[i]);
                        break;
                }
            }
            return sb.ToString();
        }

        static List<string> GetLines(string description)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(description))
            {
                lines.Add(string.Empty);
                return lines;
            }
            var length = 80 - OptionWidth - 2;
            int start = 0, end;
            do
            {
                end = GetLineEnd(start, length, description);
                var cont = false;
                if (end < description.Length)
                {
                    var c = description[end];
                    if (c == '-' || (char.IsWhiteSpace(c) && c != '\n'))
                        ++end;
                    else if (c != '\n')
                    {
                        cont = true;
                        --end;
                    }
                }
                lines.Add(description.Substring(start, end - start));
                if (cont)
                {
                    lines[lines.Count - 1] += "-";
                }
                start = end;
                if (start < description.Length && description[start] == '\n')
                    ++start;
            } while (end < description.Length);
            return lines;
        }

        static int GetLineEnd(int start, int length, string description)
        {
            var end = Math.Min(start + length, description.Length);
            var sep = -1;
            for (var i = start; i < end; ++i)
            {
                switch (description[i])
                {
                    case ' ':
                    case '\t':
                    case '\v':
                    case '-':
                    case ',':
                    case '.':
                    case ';':
                        sep = i;
                        break;
                    case '\n':
                        return i;
                }
            }
            if (sep == -1 || end == description.Length)
                return end;
            return sep;
        }
    }
}