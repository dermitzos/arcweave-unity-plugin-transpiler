using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System.Collections.Generic;
using System.Linq;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;
using Arcweave;
using System;
using System.Reflection;
using Antlr4.Runtime.Tree;
using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Xml.Linq;
using System.Reflection.Emit;

namespace Arcweave.Transpiler
{
    public class ArcscriptVisitor : ArcscriptParserBaseVisitor<Dictionary<string, object>>
    {
        public Project project = null;
        public ArcscriptState state = null;
        public string elementId = null;
        private Functions functions = null;
        public ArcscriptVisitor(string elementId, Project project)
        {
            this.elementId = elementId;
            this.project = project;
            this.state = new ArcscriptState(elementId, project);
            this.functions = new Functions(elementId, project, this.state);
        }

        public override Dictionary<string, object> VisitInput([NotNull] ArcscriptParser.InputContext context)
        {
            if (context.script() != null)
            {
                return new Dictionary<string, object>() { { "script", this.VisitScript(context.script()) } };
            }

            return new Dictionary<string, object>() { { "condition", this.VisitCompound_condition_or(context.compound_condition_or())["result"] } };
        }

        public override Dictionary<string, object> VisitScript_section([NotNull] ArcscriptParser.Script_sectionContext context)
        {
            if (context == null)
            {
                return null;
            }
            if (context.NORMALTEXT() != null && context.NORMALTEXT().Length > 0)
            {
                this.state.outputs.Add(context.GetText());
                return new Dictionary<string, object>() { { "value", context.GetText() } };
            }

            return this.VisitChildren(context);
        }

        public override Dictionary<string, object> VisitAssignment_segment([NotNull] ArcscriptParser.Assignment_segmentContext context)
        {
            return this.VisitStatement_assignment(context.statement_assignment());
        }

        public override Dictionary<string, object> VisitFunction_call_segment([NotNull] ArcscriptParser.Function_call_segmentContext context)
        {
            return this.VisitStatement_function_call(context.statement_function_call());
        }

        public override Dictionary<string, object> VisitConditional_section([NotNull] ArcscriptParser.Conditional_sectionContext context)
        {
            Dictionary<string, object> if_section = this.VisitIf_section(context.if_section());
            if (if_section != null)
            {
                return if_section;
            }
            var result = context.else_if_section().FirstOrDefault(else_if_section =>
            {
                Dictionary<string, object> elif_section = this.VisitElse_if_section(else_if_section);
                if (elif_section != null)
                {
                    return true;
                }
                return false;
            });

            if (result != null)
            {
                return new Dictionary<string, object>() { { "value", result } }; ;
            }

            if (context.else_section() != null)
            {
                return this.VisitElse_section(context.else_section());
            }
            return null;
        }

        public override Dictionary<string, object> VisitIf_section([NotNull] ArcscriptParser.If_sectionContext context)
        {
            Dictionary<string, object> result = this.VisitIf_clause(context.if_clause());
            foreach (var item in result)
            {
                Debug.Log($"{item.Key}: {item.Value}");
            }
            if ((bool) result["result"])
            {
                return this.VisitScript(context.script());
            }
            return null;
        }

        public override Dictionary<string, object> VisitElse_if_section([NotNull] ArcscriptParser.Else_if_sectionContext context)
        {
            Dictionary<string, object> result = this.VisitElse_if_clause(context.else_if_clause());
            foreach (var item in result)
            {
                Debug.Log($"{item.Key}: {item.Value}");
            }
            if ((bool) result["result"])
            {
                return this.VisitScript(context.script());
            }
            return null;
        }

        public override Dictionary<string, object> VisitElse_section([NotNull] ArcscriptParser.Else_sectionContext context)
        {
            return this.VisitScript(context.script());
        }

        public override Dictionary<string, object> VisitIf_clause([NotNull] ArcscriptParser.If_clauseContext context)
        {
            return this.VisitCompound_condition_or(context.compound_condition_or());
        }

        public override Dictionary<string, object> VisitElse_if_clause([NotNull] ArcscriptParser.Else_if_clauseContext context)
        {
            return this.VisitCompound_condition_or(context.compound_condition_or());
        }

        public override Dictionary<string, object> VisitStatement_assignment([NotNull] ArcscriptParser.Statement_assignmentContext context)
        {
            string variableName = context.VARIABLE().GetText();
            Variable variable = this.state.GetVariable(variableName);
            object variableValue = this.state.GetVarValue(variableName);

            Dictionary<string, object> compound_condition_or = this.VisitCompound_condition_or(context.compound_condition_or());
            double result = 0;
            if (context.ASSIGN() != null)
            {
                this.state.SetVarValue(variableName, Convert.ChangeType(variableValue, variable.type));
                return null;
            }

            double dblVariableValue = (double) Convert.ChangeType(variableValue, typeof(double));
            double dblCompoundCondition = (double)Convert.ChangeType(compound_condition_or["result"], typeof(double));

            if (context.ASSIGNADD() != null)
            {
                result = dblVariableValue + dblCompoundCondition;
            }
            else if (context.ASSIGNSUB() != null)
            {
                result = dblVariableValue - dblCompoundCondition;
            }
            else if (context.ASSIGNMUL() != null)
            {
                result = dblVariableValue * dblCompoundCondition;
            }
            else if (context.ASSIGNDIV() != null)
            {
                result = dblVariableValue / dblCompoundCondition;
            }

            this.state.SetVarValue(variableName, Convert.ChangeType(result, variable.type));
            return null;
        }

        public override Dictionary<string, object> VisitVoid_function_call([NotNull] ArcscriptParser.Void_function_callContext context)
        {
            string fname = "";
            object argument_list_result = null;
            if (context.VFNAME() != null)
            {
                fname = context.VFNAME().GetText();
                if (context.argument_list() != null)
                {
                    Dictionary<string, object> result = this.VisitArgument_list(context.argument_list());
                    argument_list_result = result["argument_list"];
                }
            }
            if (context.VFNAMEVARS() != null)
            {
                fname = context.VFNAMEVARS().GetText();
                if (context.variable_list() != null)
                {
                    Dictionary<string, object> result = this.VisitVariable_list(context.variable_list());
                    argument_list_result = result["variable_list"];
                }
            }
            List<Argument> argument_list = ((IList)argument_list_result)?.Cast<Argument>().ToList();
            if (argument_list == null)
            {
                argument_list = new List<Argument>() { };
            }
            Type resultType = this.functions.returnTypes[fname];
            object returnValue = this.functions.functions[fname](argument_list);

            return new Dictionary<string, object>() { { "result", returnValue } };
        }

        public override Dictionary<string, object> VisitVariable_list([NotNull] ArcscriptParser.Variable_listContext context)
        {
            List<Argument> variables = new List<Argument>();
            foreach(ITerminalNode variable in context.VARIABLE() )
            {
                Variable varObject = this.state.GetVariable(variable.GetText());
                Argument arg = new Argument(typeof (Variable), varObject);
                variables.Add(arg);
            }
            return new Dictionary<string, object>() { { "variable_list", variables } };
        }

        public override Dictionary<string, object> VisitCompound_condition_or([NotNull] ArcscriptParser.Compound_condition_orContext context)
        {
            Dictionary<string, object> compound_condition_and = this.VisitCompound_condition_and(context.compound_condition_and());
            if (context.compound_condition_or() != null)
            {
                Dictionary<string, object> compound_condition_or = this.VisitCompound_condition_or(context.compound_condition_or());
                bool result = (bool)compound_condition_and["result"] || (bool) compound_condition_or["result"];
                return new Dictionary<string, object>() { { "result",  result } };
            }
            return compound_condition_and;
        }

        public override Dictionary<string, object> VisitCompound_condition_and([NotNull] ArcscriptParser.Compound_condition_andContext context)
        {
            Dictionary<string, object> negated_unary_condition = this.VisitNegated_unary_condition(context.negated_unary_condition());
            if (context.compound_condition_and() != null)
            {
                Dictionary<string, object> compound_condition_and = this.VisitCompound_condition_and(context.compound_condition_and());
                bool result = (bool)negated_unary_condition["result"] && (bool)compound_condition_and["result"];
                return new Dictionary<string, object>() { { "result", result } };
            }

            return negated_unary_condition;
        }

        public override Dictionary<string, object> VisitNegated_unary_condition([NotNull] ArcscriptParser.Negated_unary_conditionContext context)
        {
            Dictionary<string, object> unary_condition = this.VisitUnary_condition(context.unary_condition());

            if (context.NEG() != null || context.NOTKEYWORD() != null)
            {
                return new Dictionary<string, object>() { { "result", !(bool)unary_condition["result"] } };
            }

            return unary_condition;
        }

        public override Dictionary<string, object> VisitUnary_condition([NotNull] ArcscriptParser.Unary_conditionContext context)
        {
            return this.VisitCondition(context.condition());
        }

        public override Dictionary<string, object> VisitCondition([NotNull] ArcscriptParser.ConditionContext context)
        {
            if (context.expression().Length == 1)
            {
                Dictionary<string, object> expr = this.VisitExpression(context.expression()[0]);
                if ((Type) expr["resultType"] == typeof (double))
                {
                    expr["result"] = (double)expr["result"] > 0;
                }
                else if ((Type) expr["resultType"] == typeof(int))
                {
                    expr["result"] = (int)expr["result"] > 0;
                }
                else if ((Type)expr["resultType"] == typeof(string))
                {
                    expr["result"] = (string)expr["result"] == "";
                }
                expr["resultType"] = typeof (bool);
                Debug.Log(expr["result"]);
                return expr;
            }
            ArcscriptParser.Conditional_operatorContext conditional_operator_context = context.conditional_operator();
            Dictionary<string, object> exp0 = this.VisitExpression(context.expression()[0]);
            Dictionary<string, object> exp1 = this.VisitExpression(context.expression()[1]);
            
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (conditional_operator_context.GT() != null)
            {
                result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) > (double)Convert.ChangeType(exp1["result"], typeof(double)));
                return result;
            }
            if (conditional_operator_context.GE() != null)
            {
                result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) >= (double)Convert.ChangeType(exp1["result"], typeof(double)));
                return result;
            }
            if (conditional_operator_context.LT() != null)
            {
                result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) < (double)Convert.ChangeType(exp1["result"], typeof(double)));
                return result;
            }
            if (conditional_operator_context.LE() != null)
            {
                result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) <= (double)Convert.ChangeType(exp1["result"], typeof(double)));
                return result;
            }
            if (conditional_operator_context.EQ() != null)
            {
                result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) == (double)Convert.ChangeType(exp1["result"], typeof(double)));
                return result;
            }
            if (conditional_operator_context.NE() != null)
            {
                result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) != (double)Convert.ChangeType(exp1["result"], typeof(double)));
                return result;
            }
            if (conditional_operator_context.ISKEYWORD() != null)
            {
                if (conditional_operator_context.NOTKEYWORD() != null)
                {
                    result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) != (double)Convert.ChangeType(exp1["result"], typeof(double)));
                    return result;
                }

                result.Add("result", (double)Convert.ChangeType(exp0["result"], typeof(double)) == (double)Convert.ChangeType(exp1["result"], typeof(double)));
                return result;
            }
            return this.VisitChildren(context);
        }

        public override Dictionary<string, object> VisitExpression([NotNull] ArcscriptParser.ExpressionContext context)
        {
            if (context.STRING() != null)
            {
                string result = context.STRING().GetText();
                result = result.Substring(1, result.Length - 2);
                return new Dictionary<string, object>() { { "result", result }, { "resultType", typeof(string) } };
            }
            if (context.BOOLEAN() != null)
            {
                return new Dictionary<string, object>() { { "result", context.BOOLEAN().GetText() == "true" }, { "resultType", typeof(bool) } };
            }
            return this.VisitAdditive_numeric_expression(context.additive_numeric_expression());
        }

        public override Dictionary<string, object> VisitAdditive_numeric_expression([NotNull] ArcscriptParser.Additive_numeric_expressionContext context)
        {
            Dictionary<string, object> mult_num_expression = this.VisitMultiplicative_numeric_expression(context.multiplicative_numeric_expression());

            if (context.additive_numeric_expression() != null)
            {
                Dictionary<string, object> result = this.VisitAdditive_numeric_expression(context.additive_numeric_expression());
                if (context.ADD() != null)
                {
                    return new Dictionary<string, object>() { { "result", (double)mult_num_expression["result"] + (double)result["result"] }, { "resultType", typeof (double) } };
                }
                return new Dictionary<string, object>() { { "result", (double)mult_num_expression["result"] - (double)result["result"] }, { "resultType", typeof(double) } };
            }

            return mult_num_expression;
        }

        public override Dictionary<string, object> VisitMultiplicative_numeric_expression([NotNull] ArcscriptParser.Multiplicative_numeric_expressionContext context)
        {
            Dictionary<string, object> signed_unary_num_expr = this.VisitSigned_unary_numeric_expression(context.signed_unary_numeric_expression());

            if (context.multiplicative_numeric_expression() != null)
            {
                Dictionary<string, object> result = this.VisitMultiplicative_numeric_expression(context.multiplicative_numeric_expression());
                if (context.MUL() != null)
                {
                    return new Dictionary<string, object>() { { "result", (double)signed_unary_num_expr["result"] * (double)result["result"] }, { "resultType", typeof(double) } };
                }
                // Else DIV
                return new Dictionary<string, object>() { { "result", (double)signed_unary_num_expr["result"] / (double)result["result"] }, { "resultType", typeof(double) } };
            }

            return signed_unary_num_expr;
        }

        public override Dictionary<string, object> VisitSigned_unary_numeric_expression([NotNull] ArcscriptParser.Signed_unary_numeric_expressionContext context)
        {
            Dictionary<string, object> unary_num_expr = this.VisitUnary_numeric_expression(context.unary_numeric_expression());
            ArcscriptParser.SignContext sign = context.sign();

            if (sign != null)
            {
                if (sign.ADD() != null)
                {
                    return new Dictionary<string, object>() { { "result", +(double)unary_num_expr["result"] }, { "resultType", typeof(double) } };
                }
                return new Dictionary<string, object>() { { "result", -(double)unary_num_expr["result"] }, { "resultType", typeof(double) } };
            }
            return unary_num_expr;
        }

        public override Dictionary<string, object> VisitUnary_numeric_expression([NotNull] ArcscriptParser.Unary_numeric_expressionContext context)
        {
            if (context.FLOAT() != null)
            {
                return new Dictionary<string, object>() 
                { 
                    { "result", double.Parse(context.FLOAT().GetText()) },
                    { "resultType", typeof (double) },
                };
            }
            if (context.INTEGER() != null)
            {
                return new Dictionary<string, object>() 
                { 
                    { "result", int.Parse(context.INTEGER().GetText()) },
                    { "resultType", typeof (int) },
                };
            }
            if (context.VARIABLE() != null)
            {
                string variableName = context.VARIABLE().GetText();
                object value = this.state.GetVarValue(variableName);
                return new Dictionary<string, object>() 
                { 
                    { "result", value },
                    { "resultType", this.state.GetVariable(variableName).type },
                };
            }

            if (context.function_call() != null)
            {
                Dictionary<string, object> result = this.VisitFunction_call(context.function_call());
                return result;
            }
            return this.VisitCompound_condition_or(context.compound_condition_or());
        }

        public override Dictionary<string, object> VisitFunction_call([NotNull] ArcscriptParser.Function_callContext context)
        {
            object argument_list_result = null;
            if (context.argument_list() != null)
            {
                Dictionary<string, object> result = this.VisitArgument_list(context.argument_list());
                argument_list_result = result["argument_list"];
            }

            List<Argument> argument_list = ((IList)argument_list_result)?.Cast<Argument>().ToList();
            
            if (argument_list == null)
            {
                argument_list = new List<Argument>() { };
            }
            string fname = context.FNAME().GetText();

            Type resultType = this.functions.returnTypes[fname];
            object returnValue = this.functions.functions[fname](argument_list);

            return new Dictionary<string, object>() { { "result", returnValue }, { "resultType", resultType }, { "function", fname } };
        }

        public override Dictionary<string, object> VisitArgument_list([NotNull] ArcscriptParser.Argument_listContext context)
        {
            List<object> argumentList = new List<object>();
            foreach(ArcscriptParser.ArgumentContext argument in context.argument())
            {
                argumentList.Add(this.VisitArgument(argument)["result"]);
            }
            return new Dictionary<string, object> { { "argument_list", argumentList } };
        }

        public override Dictionary<string, object> VisitArgument([NotNull] ArcscriptParser.ArgumentContext context)
        {
            if (context.STRING()  != null)
            {
                string result = context.STRING().GetText();
                result = result.Substring(1, result.Length - 2);
                return new Dictionary<string, object>() { { "result", new Argument(typeof(string), result) } };
            }
            if (context.mention() != null)
            {
                Dictionary<string, object> mention_result = this.VisitMention(context.mention());
                Argument argument = new Argument(typeof (Mention), mention_result["result"]);
                return new Dictionary<string, object>() { { "result", argument } };
            }
            Dictionary<string, object> num_expr_result = this.VisitAdditive_numeric_expression(context.additive_numeric_expression());
            return new Dictionary<string, object>() { { "result", new Argument(typeof(double), num_expr_result["result"]) } };
        }

        public override Dictionary<string, object> VisitMention([NotNull] ArcscriptParser.MentionContext context)
        {
            Dictionary<string, string> attrs = new Dictionary<string, string>();

            foreach(ArcscriptParser.Mention_attributesContext attr in context.mention_attributes())
            {
                Dictionary<string, object> res = this.VisitMention_attributes(attr);
                attrs.Add((string) res["name"], (string) res["value"]);
            }
            string label = "";
            if (context.MENTION_LABEL() != null)
            {
                label = context.MENTION_LABEL().GetText();
            }
            return new Dictionary<string, object> { { "result", new Mention(label, attrs) }, { "type", typeof(Mention) } };
        }

        public override Dictionary<string, object> VisitMention_attributes([NotNull] ArcscriptParser.Mention_attributesContext context)
        {
            string name = context.ATTR_NAME().GetText();
            ITerminalNode ctxvalue = context.ATTR_VALUE();
            object value = true;
            if (ctxvalue != null)
            {
                string strvalue = ctxvalue.GetText();
                if ((strvalue.StartsWith('"') && strvalue.EndsWith('"')) ||
                    (strvalue.StartsWith("'") && strvalue.EndsWith("'")))
                {
                    strvalue = strvalue.Substring(1, strvalue.Length - 2);
                }
                value = strvalue;
            }
            return new Dictionary<string, object> { { "name", name }, { "value", value } };
        }

        public class Argument
        {
            public Type type { get; private set; }
            public object value { get; private set; }
            public Argument(Type type, object value)
            {
                this.type = type;
                this.value = value;
            }
        }

        public class Mention
        {
            public string label { get; private set; }
            public Dictionary<string, string> attrs { get; private set; }
            
            public Mention(string label, Dictionary<string, string> attrs)
            {
                this.label = label;
                this.attrs = attrs;
            }
        }
    }
}