using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Arcweave.Transpiler
{
    public class Functions
    {
        private static readonly Random _getrandom = new Random();
        private Project _project;
        private string elementId;
        private ArcscriptState state;
        public Dictionary<string, Func<IList<object>, object>> functions { get; private set; } = new Dictionary<string, Func<IList<object>, object>>();

        public Dictionary<string, Type> returnTypes = new Dictionary<string, Type>()
        {
            { "sqrt",  typeof (double) },
            { "sqr", typeof (double) },
            { "abs", typeof (double) },
            { "random", typeof (double) },
            { "roll", typeof (int) },
            { "show", typeof (string) },
            { "reset", typeof (void) },
            { "resetAll", typeof (void) },
            { "round", typeof (int) },
            { "min", typeof (double) },
            { "max", typeof (double) },
            { "visits", typeof (int) }
        };
        public Functions(string elementId, Project project, ArcscriptState state) { 
            this._project = project;
            this.elementId = elementId;
            this.state = state;

            this.functions["sqrt"] = this.Sqrt;
            this.functions["sqr"] = this.Sqr;
            this.functions["abs"] = this.Abs;
            this.functions["random"] = this.Random;
            this.functions["roll"] = this.Roll;
            this.functions["show"] = this.Show;
            this.functions["reset"] = this.Reset;
            this.functions["resetAll"] = this.ResetAll;
            this.functions["round"] = this.Round;
            this.functions["min"] = this.Min;
            this.functions["max"] = this.Max;
            this.functions["visits"] = this.Visits;
        }

        public object Sqrt(IList<object> args)
        {
            double n = (double)args[0];
            return Math.Sqrt(n);
        }

        public object Sqr(IList<object> args)
        {
            double n = (double)args[0];
            return n * n;
        }

        public object Abs(IList<object> args) 
        { 
            double n = (double)args[0];
            return Math.Abs(n); 
        }

        public object Random(IList<object> args) { 
            lock ( _getrandom )
            {
                return _getrandom.NextDouble(); 
            }
        }

        public object Roll(IList<object> args)
        {
            int maxRoll = (int)args[0];
            int numRolls = 1;
            if (args.Count == 2)
            {
                numRolls = (int) args[1];
            }
            int sum = 0;
            for (int i = 0; i < numRolls; i++)
            {
                int oneRoll = _getrandom.Next(1, maxRoll + 1);
                sum += oneRoll;
            }
            return sum;
        }

        public object Show(IList<object> args)
        {
            List<object> results = new List<object>();
            foreach (object o in args)
            {
                Dictionary<string, object> arg = o as Dictionary<string, object>;
                results.Add(arg["result"].ToString());
            }
            string result = String.Join(' ', results.ToArray());
            UnityEngine.Debug.Log(result);
            this.state.outputs.Add(result);
            return null;
        }

        public object Reset(IList<object> args)
        {
            return null;
        }

        public object ResetAll(IList<object> args)
        {
            return null;
        }

        public object Round(IList<object> args)
        {
            double n = (double)args[0];
            return (int)Math.Round(n);
        }

        public object Min(IList<object> args)
        {
            return args.Min();
        }

        public object Max(IList<object> args)
        {
            return args.Max();
        }

        public object Visits(IList<object> args)
        {
            string elementId = this.elementId;
            if (args != null && args.Count == 1)
            {
                Dictionary<string, object> mention = (Dictionary<string, object>) args[0];
                Dictionary<string, string> mentionAttrs = (Dictionary<string, string>)mention["attrs"];
                elementId = mentionAttrs["data-id"];
            }
            Element element = this._project.ElementWithID(elementId);
            UnityEngine.Debug.Log(element.visits);
            return element.visits;
        }
    }
}