/* ====================================================================
   Licensed To the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file To You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed To in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */

namespace Npoi.Core.SS.Formula
{
    using Npoi.Core.SS.UserModel;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;

    [Serializable]
    public class WorkbookNotFoundException : Exception
    {
        public WorkbookNotFoundException(string msg) : base(msg)
        {
        }
    }

    /**
     * Manages a collection of {@link WorkbookEvaluator}s, in order To support evaluation of formulas
     * across spreadsheets.<p/>
     *
     * For POI internal use only
     *
     * @author Josh Micich
     */

    public class CollaboratingWorkbooksEnvironment
    {
        public static readonly CollaboratingWorkbooksEnvironment EMPTY = new CollaboratingWorkbooksEnvironment();

        private Dictionary<string, WorkbookEvaluator> _evaluatorsByName;
        private WorkbookEvaluator[] _evaluators;

        private bool _unhooked;

        private CollaboratingWorkbooksEnvironment()
        {
            _evaluatorsByName = new Dictionary<string, WorkbookEvaluator>();
            _evaluators = new WorkbookEvaluator[0];
        }

        public static void Setup(string[] workbookNames, WorkbookEvaluator[] evaluators)
        {
            int nItems = workbookNames.Length;
            if (evaluators.Length != nItems)
            {
                throw new ArgumentException("Number of workbook names is " + nItems
                        + " but number of evaluators is " + evaluators.Length);
            }
            if (nItems < 1)
            {
                throw new ArgumentException("Must provide at least one collaborating worbook");
            }
            CollaboratingWorkbooksEnvironment env = new CollaboratingWorkbooksEnvironment(workbookNames, evaluators, nItems);
            HookNewEnvironment(evaluators, env);
        }

        public static void Setup(Dictionary<string, WorkbookEvaluator> evaluatorsByName)
        {
            if (evaluatorsByName.Count < 1)
            {
                throw new ArgumentException("Must provide at least one collaborating worbook");
            }
            List<WorkbookEvaluator> temp = new List<WorkbookEvaluator>(evaluatorsByName.Count);
            temp.AddRange(evaluatorsByName.Values);
            WorkbookEvaluator[] evaluators = temp.ToArray();
            new CollaboratingWorkbooksEnvironment(evaluatorsByName, evaluators);
        }

        public static void SetupFormulaEvaluator(Dictionary<string, IFormulaEvaluator> evaluators)
        {
            Dictionary<string, WorkbookEvaluator> evaluatorsByName = new Dictionary<string, WorkbookEvaluator>(evaluators.Count);
            foreach (string wbName in evaluators.Keys)
            {
                IFormulaEvaluator eval = evaluators[(wbName)];
                if (eval is IWorkbookEvaluatorProvider)
                {
                    evaluatorsByName.Add(wbName, ((IWorkbookEvaluatorProvider)eval).GetWorkbookEvaluator());
                }
                else
                {
                    throw new ArgumentException("Formula Evaluator " + eval +
                                                       " provides no WorkbookEvaluator access");
                }
            }
            Setup(evaluatorsByName);
        }

        private CollaboratingWorkbooksEnvironment(string[] workbookNames, WorkbookEvaluator[] evaluators, int nItems)
            : this(toUniqueMap(workbookNames, evaluators, nItems), evaluators)
        {
        }

        private static Dictionary<string, WorkbookEvaluator> toUniqueMap(string[] workbookNames, WorkbookEvaluator[] evaluators, int nItems)
        {
            Dictionary<string, WorkbookEvaluator> evaluatorsByName = new Dictionary<string, WorkbookEvaluator>(nItems * 3 / 2);
            for (int i = 0; i < nItems; i++)
            {
                string wbName = workbookNames[i];
                WorkbookEvaluator wbEval = evaluators[i];
                if (evaluatorsByName.ContainsKey(wbName))
                {
                    throw new ArgumentException("Duplicate workbook name '" + wbName + "'");
                }
                evaluatorsByName.Add(wbName, wbEval);
            }
            return evaluatorsByName;
        }

        private CollaboratingWorkbooksEnvironment(Dictionary<string, WorkbookEvaluator> evaluatorsByName, WorkbookEvaluator[] evaluators)
        {
            Dictionary<WorkbookEvaluator, string> uniqueEvals = new Dictionary<WorkbookEvaluator, string>(evaluators.Length);
            foreach (string wbName in evaluatorsByName.Keys)
            {
                WorkbookEvaluator wbEval = evaluatorsByName[(wbName)];
                if (uniqueEvals.ContainsKey(wbEval))
                {
                    string msg = "Attempted to register same workbook under names '"
                        + uniqueEvals[(wbEval)] + "' and '" + wbName + "'";
                    throw new ArgumentException(msg);
                }
                uniqueEvals.Add(wbEval, wbName);
            }
            UnhookOldEnvironments(evaluators);
            HookNewEnvironment(evaluators, this);
            _unhooked = false;
            _evaluators = evaluators;
            _evaluatorsByName = evaluatorsByName;
        }

        private static void HookNewEnvironment(WorkbookEvaluator[] evaluators, CollaboratingWorkbooksEnvironment env)
        {
            // All evaluators will need To share the same cache.
            // but the cache takes an optional evaluation listener.
            int nItems = evaluators.Length;
            IEvaluationListener evalListener = evaluators[0].GetEvaluationListener();
            // make sure that all evaluators have the same listener
            for (int i = 0; i < nItems; i++)
            {
                if (evalListener != evaluators[i].GetEvaluationListener())
                {
                    // This would be very complex To support
                    throw new Exception("Workbook evaluators must all have the same evaluation listener");
                }
            }
            EvaluationCache cache = new EvaluationCache(evalListener);

            for (int i = 0; i < nItems; i++)
            {
                evaluators[i].AttachToEnvironment(env, cache, i);
            }
        }

        private void UnhookOldEnvironments(WorkbookEvaluator[] evaluators)
        {
            List<object> oldEnvs = new List<object>();
            for (int i = 0; i < evaluators.Length; i++)
            {
                oldEnvs.Add(evaluators[i].GetEnvironment());
            }
            CollaboratingWorkbooksEnvironment[] oldCWEs = new CollaboratingWorkbooksEnvironment[oldEnvs.Count];
            oldCWEs = (CollaboratingWorkbooksEnvironment[])oldEnvs.ToArray();
            for (int i = 0; i < oldCWEs.Length; i++)
            {
                oldCWEs[i].Unhook();
            }
        }

        /**
         *
         */

        private void Unhook()
        {
            if (_evaluators.Length < 1)
            {
                return;
            }
            for (int i = 0; i < _evaluators.Length; i++)
            {
                _evaluators[i].DetachFromEnvironment();
            }
            _unhooked = true;
        }

        public WorkbookEvaluator GetWorkbookEvaluator(string workbookName)
        {
            if (_unhooked)
            {
                throw new InvalidOperationException("This environment Has been unhooked");
            }
            WorkbookEvaluator result;
            if (_evaluatorsByName.ContainsKey(workbookName))
            {
                result = (WorkbookEvaluator)_evaluatorsByName[workbookName];
            }
            else
            {
                StringBuilder sb = new StringBuilder(256);
                sb.Append("Could not resolve external workbook name '").Append(workbookName).Append("'.");
                if (_evaluators.Length < 1)
                {
                    sb.Append(" Workbook environment has not been set up.");
                }
                else
                {
                    sb.Append(" The following workbook names are valid: (");
                    IEnumerator i = _evaluatorsByName.Keys.GetEnumerator();
                    int count = 0;
                    while (i.MoveNext())
                    {
                        if (count++ > 0)
                        {
                            sb.Append(", ");
                        }
                        sb.Append("'").Append(i.Current).Append("'");
                    }
                    sb.Append(")");
                }
                throw new WorkbookNotFoundException(sb.ToString());
            }
            return result;
        }
    }
}