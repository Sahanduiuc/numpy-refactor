﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Numerics;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace NumpyDotNet {
    /// <summary>
    /// Package of extension methods.
    /// </summary>
    internal static class NpyUtils_Extensions {

        /// <summary>
        /// Applies function f to all elements in 'input'. Same as Select() but
        /// with no result.
        /// </summary>
        /// <typeparam name="Tin">Element type</typeparam>
        /// <param name="input">Input sequence</param>
        /// <param name="f">Function to be applied</param>
        public static void Iter<Tin>(this IEnumerable<Tin> input, Action<Tin> f) {
            foreach (Tin x in input) {
                f(x);
            }
        }

        /// <summary>
        /// Applies function f to all elements in 'input' plus the index of each
        /// element.
        /// </summary>
        /// <typeparam name="Tin">Type of input elements</typeparam>
        /// <param name="input">Input sequence</param>
        /// <param name="f">Function to be applied</param>
        public static void Iteri<Tin>(this IEnumerable<Tin> input, Action<Tin, int> f) {
            int i = 0;
            foreach (Tin x in input) {
                f(x, i);
                i++;
            }
        }
    }


    /// <summary>
    /// A package of utilities for dealing with Python
    /// </summary>
    internal static class NpyUtil_Python
    {
        private static CodeContext defaultContext = null;

        /// <summary>
        /// The default code context is initialized at startup and is used when no code context
        /// is otherwise available.
        /// </summary>
        internal static CodeContext DefaultContext {
            get { return defaultContext; }
            set {
                if (defaultContext != null) {
                    // Disallowed only because there doesn't appear to be a reason to do this
                    // except in an error condition.
                    throw new InvalidOperationException("Attempt to re-initialize default code context.");
                }
                defaultContext = value;
            }
        }

        /// <summary>
        /// Call a Python function in numpy.core._internal
        /// </summary>
        /// <param name="cntx">Code context to use, or DefaultContext is used if null</param>
        /// <param name="func_name">Name of the function to call</param>
        /// <param name="args">Calling arguments</param>
        /// <returns>Result of Python function</returns>
        internal static object CallInternal(CodeContext cntx, string func_name, params object[] args) {
            object f;
            if (cntx == null) cntx = DefaultContext;

            PythonModule module = (PythonModule)PythonOps.ImportBottom(cntx, "numpy.core._internal", 0);
            if (!PythonOps.ModuleTryGetMember(cntx, module, func_name, out f)) {
                throw new ArgumentException(String.Format("'{0}' is not a function in numpy.core._internal.", func_name));
            }
            return PythonCalls.Call(cntx, f, args: args);
        }

        internal static object CallBuiltin(CodeContext cntx, string func_name, params object[] args) {
            if (cntx == null) cntx = DefaultContext;

            object f = cntx.LanguageContext.BuiltinModuleDict.get(func_name);
            if (f == null) {
                throw new ArgumentException(String.Format("'{0}' is not a built-in function."), func_name);
            }
            return PythonCalls.Call(cntx, f, args: args);
        }

        /// <summary>
        /// Triggers Python integer conversion using __int__ function on Python objects. int
        /// objects are handled intelligently.
        /// </summary>
        /// <param name="obj">Object to convert</param>
        /// <param name="cntx">Current code context or null to use default</param>
        /// <returns>Integer value or throws an exception if no conversion is possible</returns>
        internal static int ConvertToInt(object obj, CodeContext cntx=null) {
            if (cntx == null) cntx = DefaultContext;

            if (obj is int) {
                return (int)obj;
            } else if (obj is ScalarInt32) {
                return (int)(ScalarInt32)obj;
            } else {
                object result = CallBuiltin(cntx, "int", obj);
                if (result is int) {
                    return (int)result;
                } else {
                    throw new OverflowException();
                }
            }
        }


        /// <summary>
        /// Triggers Python conversion to long using __int__ function on Py objects. int
        /// types are handled efficiently.
        /// </summary>
        /// <param name="obj">Object to convert</param>
        /// <param name="cntx">Current code context or null to use default</param>
        /// <returns>long value or throws exception is conversion fails</returns>
        internal static long ConvertToLong(object obj, CodeContext cntx = null) {
            if (cntx == null) cntx = DefaultContext;

            if (obj is long) {
                return (long)obj;
            } else if (obj is int) {
                return (long)(int)obj;
            } else if (obj is ScalarInt64) {
                return (long)(ScalarInt64)obj;
            }

            object result = CallBuiltin(cntx, "int", obj);
            if (result is int) {
                return (long)(int)result;
            } else if (result is BigInteger) {
                BigInteger i = (BigInteger)result;
                if (i > long.MaxValue || i < long.MinValue) {
                    throw new OverflowException();
                }
                return (long)i;
            } else {
                throw new ArgumentException("__int__ did not return an int or a long");
            }
        }

        internal static bool IsCallable(object obj) {
            return PythonOps.IsCallable(DefaultContext, obj);
        }


        /// <summary>
        /// Triggers Python conversion to float using __float__ function on Py objects. float
        /// types are handled efficiently.
        /// </summary>
        /// <param name="obj">Object to convert</param>
        /// <param name="cntx">Current code context or null to use default</param>
        /// <returns>float value or throws exception is conversion fails</returns>
        internal static double ConvertToDouble(object obj, CodeContext cntx = null) {
            if (cntx == null) cntx = DefaultContext;

            if (obj is double) {
                return (double)obj;
            } else if (obj is ScalarFloat32) {
                return (double)(ScalarFloat32)obj;
            } else if (obj is ScalarFloat64) {
                return (double)(ScalarFloat64)obj;
            }

            object result = CallBuiltin(cntx, "float", obj);
            if (result is double) {
                return (double)result;
            } else {
                throw new ArgumentException("__float__ did not return a floating-point value");
            }
        }


        /// <summary>
        /// Triggers Python conversion to string using __str__ method.  String instances
        /// are handled efficiently.
        /// </summary>
        /// <param name="obj">Object to convert</param>
        /// <param name="cntx">Current code context or null to use default</param>
        /// <returns>String result or throws an exception if no conversion is possible</returns>
        internal static string ConvertToString(object obj, CodeContext cntx = null) {
            if (cntx == null) cntx = DefaultContext;

            if (obj is string) {
                return (string)obj;
            } else {
                object result = CallBuiltin(cntx, "str", obj);
                if (result is string) {
                    return (string)result;
                } else {
                    throw new ArgumentException("__str__ did not return a string");
                }
            }
        }

        internal static bool IsIntegerScalar(object o) {
            return (o is int || o is BigInteger || o is ScalarInteger);
        }

        internal static bool IsTupleOfIntegers(object o) {
            PythonTuple t = o as PythonTuple;
            if (t == null) {
                return false;
            }
            foreach (object item in t) {
                if (!IsIntegerScalar(item)) {
                    return false;
                }
            }
            return true;
        }

        internal static void Warn(PythonType category, string msg, params object[] args) {
            PythonOps.Warn(defaultContext, category, msg, args);
        }

        internal static PythonTuple ToPythonTuple(long[] array) {
            int n = array.Length;
            object[] vals = new object[n];
            // Convert to Python types
            for (int i = 0; i < n; i++) {
                long v = array[i];
                if (v < int.MinValue || v > int.MaxValue) {
                    vals[i] = new BigInteger(v);
                } else {
                    vals[i] = (int)v;
                }
            }
            // Make the tuple
            return new PythonTuple(vals);
        }

        internal static object ToPython(long l) {
            if (l < int.MinValue || l > int.MaxValue) {
                return new BigInteger(l);
            } else {
                return (int)l;
            }
        }
            
    }


    internal static class NpyUtil_ArgProcessing {

        internal static bool BoolConverter(Object o) {
            if (o == null) return false;
            else if (o is Boolean) return (bool)o;
            else if (o is IConvertible) return Convert.ToBoolean(o);

            throw new ArgumentException(String.Format("Unable to convert argument '{0}' to Boolean value.", o));
        }


        internal static int IntConverter(Object o) {
            if (o == null) return 0;
            else if (o is int) return (int)o;
            else if (o is IConvertible) return Convert.ToInt32(o);

            throw new ArgumentException(String.Format("Unable to convert argument '{0}' to int value.", o));
        }

        internal static long[] IntArrConverter(Object o) {
            if (o == null) return null;
            else if (o is IEnumerable<Object>) {
                return ((IEnumerable<Object>)o).Select(x => ((IConvertible)x).ToInt64(null)).ToArray();
            } else if (o is IConvertible) {
                return new long[1] { ((IConvertible)o).ToInt64(null) };
            } else {
                throw new NotImplementedException(
                    String.Format("Type '{0}' is not supported for array dimensions.",
                    o.GetType().Name));
            }
        }

        internal static IntPtr[] IntpArrConverter(Object o) {
            if (o == null) return null;
            else if (o is IEnumerable<Object>) {
                return ((IEnumerable<Object>)o).Select(x => IntpConverter(x)).ToArray();
            } else if (o is IConvertible) {
                return new IntPtr[] { IntpConverter(Convert.ToInt64((IConvertible)o)) };
            } else {
                throw new NotImplementedException(
                    String.Format("Type '{0}' is not supported for array dimensions.",
                    o.GetType().Name));
            }
        }

        internal static int AxisConverter(object o) {
            if (o == null) return NpyDefs.NPY_MAXDIMS;
            else if (o is int) {
                return (int)o;
            } else if (o is IConvertible) {
                return ((IConvertible)o).ToInt32(null);
            } else {
                throw new NotImplementedException(
                    String.Format("Type '{0}' is not supported for an axis.", o.GetType().Name));
            }
        }

        internal static NpyDefs.NPY_CLIPMODE ClipmodeConverter(object o) {
            if (o == null) return NpyDefs.NPY_CLIPMODE.NPY_RAISE;
            else if (o is string) {
                string s = (string)o;
                switch (s[0]) {
                    case 'C':
                    case 'c':
                        return NpyDefs.NPY_CLIPMODE.NPY_CLIP;
                    case 'W':
                    case 'w':
                        return NpyDefs.NPY_CLIPMODE.NPY_WRAP;
                    case 'r':
                    case 'R':
                        return NpyDefs.NPY_CLIPMODE.NPY_RAISE;
                    default:
                        throw new ArgumentTypeException("clipmode not understood");
                }
            } else {
                int i = IntConverter(o);
                if (i < (int)NpyDefs.NPY_CLIPMODE.NPY_CLIP || i > (int)NpyDefs.NPY_CLIPMODE.NPY_RAISE) {
                    throw new ArgumentTypeException("clipmode not understood");
                }
                return (NpyDefs.NPY_CLIPMODE)i;
            }
        }

        /// <summary>
        /// Converts an argument to an order specification.  Argument can be a string
        /// starting with 'c', 'f', or 'a' (case-insensitive), a bool type, something
        /// convertable to bool, or null.
        /// </summary>
        /// <param name="o">Order specification</param>
        /// <returns>Npy order type</returns>
        internal static NpyDefs.NPY_ORDER OrderConverter(Object o) {
            NpyDefs.NPY_ORDER order;

            if (o == null) order = NpyDefs.NPY_ORDER.NPY_ANYORDER;
            else if (o is Boolean) order = ((bool)o) ?
                         NpyDefs.NPY_ORDER.NPY_FORTRANORDER : NpyDefs.NPY_ORDER.NPY_CORDER;
            else if (o is string) {
                string s = (string)o;
                switch (s[0]) {
                    case 'C':
                    case 'c':
                        order = NpyDefs.NPY_ORDER.NPY_CORDER;
                        break;
                    case 'F':
                    case 'f':
                        order = NpyDefs.NPY_ORDER.NPY_FORTRANORDER;
                        break;
                    case 'A':
                    case 'a':
                        order = NpyDefs.NPY_ORDER.NPY_ANYORDER;
                        break;
                    default:
                        throw new ArgumentTypeException("order not understood");
                }
            } else if (o is IConvertible) {
                order = ((IConvertible)o).ToBoolean(null) ?
                    NpyDefs.NPY_ORDER.NPY_FORTRANORDER : NpyDefs.NPY_ORDER.NPY_CORDER;
            } else throw new ArgumentTypeException("order not understood");

            return order;
        }

        internal static char ByteorderConverter(string s) {
            if (s == null) {
                return 's';
            } else {
                if (s.Length == 0) {
                    throw new ArgumentException("Byteorder string must be at least length 1");
                }
                switch (s[0]) {
                    case '>':
                    case 'b':
                    case 'B':
                        return '>';
                    case '<':
                    case 'l':
                    case 'L':
                        return '<';
                    case '=':
                    case 'n':
                    case 'N':
                        return '=';
                    case 's':
                    case 'S':
                        return 's';
                    default:
                        throw new ArgumentException(String.Format("{0} is an unrecognized byte order"));
                }
            }
        }

        internal static IntPtr IntpConverter(object arg) {
            if (IntPtr.Size == 4) {
                if (arg is int) {
                    return (IntPtr)(int)arg;
                } else if (arg is IConvertible) {
                    return (IntPtr)Convert.ToInt32((IConvertible)arg);
                }
            } else {
                if (arg is long) {
                    return (IntPtr)(long)arg;
                } else if (arg is IConvertible) {
                    return (IntPtr)Convert.ToInt64((IConvertible)arg);
                }
            }
            throw new ArgumentTypeException("Argument can't be converted to an integer.");
        }

        internal static IntPtr[] IntpListConverter(IList<object> args) {
            IntPtr[] result = new IntPtr[args.Count];
            int i=0;
            foreach (object arg in args) {
                result[i++] = IntpConverter(arg);
            }
            return result;
        }


        internal static Object[] BuildArgsArray(Object[] posArgs, String[] kwds,
            IDictionary<object, object> namedArgs) {
            // For some reason the name of the attribute can only be access via ToString
            // and not as a key so we fix that here.
            if (namedArgs == null) {
                return new Object[kwds.Length];
            }

            Dictionary<String, Object> argsDict = namedArgs
                .Select(kvPair => new KeyValuePair<String, Object>(kvPair.Key.ToString(), kvPair.Value))
                .ToDictionary((kvPair => kvPair.Key), (kvPair => kvPair.Value));

            // The result, filled in as we go.
            Object[] args = new Object[kwds.Length];
            int i;

            // Copy in the position arguments.
            for (i = 0; i < posArgs.Length; i++) {
                if (argsDict.ContainsKey(kwds[i])) {
                    throw new ArgumentException(String.Format("Argument '{0}' is specified both positionally and by name.", kwds[i]));
                }
                args[i] = posArgs[i];
            }

            // Now insert any named arguments into the correct position.
            for (i = posArgs.Length; i < kwds.Length; i++) {
                if (argsDict.TryGetValue(kwds[i], out args[i])) {
                    argsDict.Remove(kwds[i]);
                } else {
                    args[i] = null;
                }
            }
            if (argsDict.Count > 0) {
                throw new ArgumentException("Unknown named arguments were specified.");
            }
            return args;
        }


        internal static NpyDefs.NPY_SORTKIND SortkindConverter(string kind) {
            if (kind == null) {
                return NpyDefs.NPY_SORTKIND.NPY_QUICKSORT;
            }
            if (kind.Length < 1) {
                throw new ArgumentException("Sort kind string must be at least length 1");
            }
            switch (kind[0]) {
                case 'q':
                case 'Q':
                    return NpyDefs.NPY_SORTKIND.NPY_QUICKSORT;
                case 'h':
                case 'H':
                    return NpyDefs.NPY_SORTKIND.NPY_HEAPSORT;
                case 'm':
                case 'M':
                    return NpyDefs.NPY_SORTKIND.NPY_MERGESORT;
                default:
                    throw new ArgumentException(String.Format("{0} is an unrecognized kind of SortedDictionary", kind));
            }
        }

        internal static NpyDefs.NPY_SEARCHSIDE SearchsideConverter(string side) {
            if (side == null) {
                return NpyDefs.NPY_SEARCHSIDE.NPY_SEARCHLEFT;
            }
            if (side.Length < 1) {
                throw new ArgumentException("Expected nonexpty string for keyword 'side'");
            }
            switch (side[0]) {
                case 'l':
                case 'L':
                    return NpyDefs.NPY_SEARCHSIDE.NPY_SEARCHLEFT;
                case 'r':
                case 'R':
                    return NpyDefs.NPY_SEARCHSIDE.NPY_SEARCHRIGHT;
                default:
                    throw new ArgumentException(String.Format("'{0}' is an InvalidCastException value for keyword 'side'", side));
            }
        }

        internal static ndarray[] ConvertToCommonType(IEnumerable<object> objs) {
            // Determine the type and size;
            // TODO: Handle scalars correctly.
            long n = 0;
            dtype intype = null;
            foreach (object o in objs) {
                intype = NpyArray.FindArrayType(o, intype, NpyDefs.NPY_MAXDIMS);
                ++n;
            }

            if (n == 0) {
                throw new ArgumentException("0-length sequence");
            }

            // Convert items to array objects
            ndarray[] result = new ndarray[n];
            n = 0;
            foreach (object o in objs) {
                result[n++] = NpyArray.FromAny(o, intype, 0, 0, NpyDefs.NPY_CARRAY, null);
            }
            return result;
        }
    }


    internal static class NpyUtil_IndexProcessing
    {
        public static void IndexConverter(Object[] indexArgs, NpyIndexes indexes)
        {
            if (indexArgs.Length != 1) {
                // This is the simple case. Just convert each arg.
                if (indexArgs.Length > NpyCoreApi.IndexInfo.max_dims) {
                    throw new IndexOutOfRangeException("Too many indices");
                }
                foreach (object arg in indexArgs) {
                    ConvertSingleIndex(arg, indexes);
                }
            } else {
                // Single index.
                object arg = indexArgs[0];
                if (arg is ndarray) {
                    ConvertSingleIndex(arg, indexes);
                } else if (arg is string) {
                    ConvertSingleIndex(arg, indexes);
                } else if (arg is IEnumerable<object> && SequenceTuple((IEnumerable<object>)arg)) {
                    foreach (object sub in (IEnumerable<object>)arg) {
                        ConvertSingleIndex(sub, indexes);
                    }
                } else {
                    ConvertSingleIndex(arg, indexes);
                }
            }
        }

        /// <summary>
        /// Determines whether or not to treat the sequence as multiple indexes
        /// We do this unless it looks like a sequence of indexes.
        /// </summary>
        private static bool SequenceTuple(IEnumerable<object> seq)
        {
            if (seq.Count() > NpyCoreApi.IndexInfo.max_dims)
                return false;

            foreach (object arg in seq)
            {
                if (arg == null ||
                    arg is IronPython.Runtime.Types.Ellipsis ||
                    arg is ISlice ||
                    arg is IEnumerable<object>)
                    return true;
            }
            return false;
        }

        private static void ConvertSingleIndex(Object arg, NpyIndexes indexes)
        {
            if (arg == null)
            {
                indexes.AddNewAxis();
            }
            else if (arg is IronPython.Runtime.Types.Ellipsis)
            {
                indexes.AddEllipsis();
            }
            else if (arg is bool)
            {
                indexes.AddIndex((bool)arg);
            }
            else if (arg is int)
            {
                indexes.AddIndex((IntPtr)(int)arg);
            }
            else if (arg is BigInteger)
            {
                BigInteger bi = (BigInteger)arg;
                long lval = (long)bi;
                indexes.AddIndex((IntPtr)lval);
            }
            else if (arg is ISlice)
            {
                indexes.AddIndex((ISlice)arg);
            }
            else if (arg is string) {
                indexes.AddIndex((string)arg);
            }
            else
            {
                ndarray array_arg = arg as ndarray;

                // Boolean scalars
                if (array_arg != null &&
                    array_arg.ndim == 0 &&
                    NpyDefs.IsBool(array_arg.dtype.TypeNum))
                {
                    indexes.AddIndex(Converter.ConvertToBoolean(array_arg));
                }
                // Integer scalars
                else if (array_arg != null &&
                    array_arg.ndim == 0 &&
                    NpyDefs.IsInteger(array_arg.dtype.TypeNum))
                {
                    indexes.AddIndex((IntPtr)Converter.ConvertToInt64(array_arg));
                }
                else if (array_arg != null)
                {
                    // Arrays must be either boolean or integer.
                    if (NpyDefs.IsInteger(array_arg.dtype.TypeNum))
                    {
                        indexes.AddIntpArray(array_arg);
                    }
                    else if (NpyDefs.IsBool(array_arg.dtype.TypeNum))
                    {
                        indexes.AddBoolArray(array_arg);
                    }
                    else
                    {
                        throw new IndexOutOfRangeException("arrays used as indices must be of integer (or boolean) type.");
                    }
                }
                else if (arg is IEnumerable<Object>)
                {
                    // Other sequences we convert to an intp array
                    indexes.AddIntpArray(arg);
                }
                else if (arg is IConvertible)
                {
                    if (IntPtr.Size == 4)
                    {
                        indexes.AddIndex((IntPtr)Convert.ToInt32(arg));
                    }
                    else
                    {
                        indexes.AddIndex((IntPtr)Convert.ToInt64(arg));
                    }
                }
                else
                {
                    throw new ArgumentException(String.Format("Argument '{0}' is not a valid index.", arg));
                }
            }
        }
    }
}