using System.Net.Mail;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LLVMSharp;
using Microsoft.VisualBasic.CompilerServices;
using Module = System.Reflection.Module;

namespace ArctCompiler;

public class MainAST
{
    private static Dictionary<string, FunctionAST> functionList = new Dictionary<string, FunctionAST>();
    
    

    public void Add_Function(string name,FunctionAST pointer)
    {
        functionList.Add(name, pointer);
    }
    public FunctionAST get_function(string name)
    {
        return functionList[name];
    }
}

public class FunctionAST
{
    private static Dictionary<string, object> variableList = new Dictionary<string, object>();

    public string Name { get; private set; }
    public MainAST MainAST { get; private set; }
    public object Function { get; private set; }

    public FunctionAST(string name, MainAST mainAst, object function)
    {
        Name = name;
        MainAST = mainAst;
        Function = function;
        mainAst.Add_Function(Name, this);
    }

    public void AddVariable(string name, LLVMValueRef pointer)
    {
        variableList[name] = pointer;
    }

    public LLVMValueRef GetVariable(string name)
    {
        return (LLVMValueRef)variableList[name];
    }
}


public class OverrideListener : arctBaseListener
{
    
    public LLVMModuleRef Module { get; set; }
    public MainAST AST { get; set; }
    public OverrideListener()
    {
        Module = LLVM.ModuleCreateWithName("Try");
        AST = new MainAST();
        
      
    }
    
    public override void EnterFunction(arctParser.FunctionContext context)
    {
        FunctionListener listener = new FunctionListener(this.Module,this.AST);
        ParseTreeWalker.Default.Walk(listener, context);
    }
}

public class FunctionListener : arctBaseListener
{
    public LLVMModuleRef Module { get; set; }
    public MainAST MainAst { get; set; }
    public LLVMValueRef? Functions;
    public FunctionAST? Ast;
    public LLVMBuilderRef Builder;
    public FunctionListener(LLVMModuleRef module, MainAST mainAst)
    {
        Module = module;
        MainAst = mainAst;
        Ast = null;
        Functions = null;
    }
    public override void EnterFunctionHead(arctParser.FunctionHeadContext context)
    {
        var return_type = context.type().GetText() == "int" ? LLVMTypeRef.Int32Type() : LLVMTypeRef.DoubleType();
        List<LLVMTypeRef> args_type = new List<LLVMTypeRef>();
        foreach (var arg in context.arguments().arg())
        {
            args_type.Add( arg.type().GetText() == "int" ? 
                LLVMTypeRef.Int32Type() :
                LLVMTypeRef.DoubleType());
        }
        
        LLVMTypeRef[] paramTypes = args_type.ToArray();
        string func_name = context.ID().Symbol.Text;
        LLVMTypeRef ret_type = LLVM.FunctionType(return_type, paramTypes, false);
        Functions = LLVM.AddFunction(Module, func_name, ret_type);
        LLVMBasicBlockRef entry = LLVM.AppendBasicBlock((LLVMValueRef)Functions, "entry");
        this.Builder = LLVM.CreateBuilder();
        LLVM.PositionBuilderAtEnd(Builder, entry);
        
        
        var ptrArg = LLVM.GetNamedFunction(Module, func_name);
        Ast = new FunctionAST(func_name, MainAst, Functions);
        
        List<string> args = new List<string>();
        foreach (var arg in context.arguments().arg())
        {
            args.Add(arg.ID().GetText());
        }
        
        var ptrArgParams = ptrArg.GetParams();

        foreach (var i in Enumerable.Range(0,ptrArgParams.Length))
        {
            Ast.AddVariable(args[i],ptrArgParams[i]);
        }

    }
    
    public override void EnterFunctionBody(arctParser.FunctionBodyContext context)
    {
        FunctionBodyListener listener = new FunctionBodyListener(this.Builder, Ast, this.Module, this.Functions);
        ParseTreeWalker.Default.Walk(listener, context);
        
    }

    public override void ExitFunctionBody(arctParser.FunctionBodyContext context)
    {
        // LLVM.SetLinkage(main, LLVMLinkage.LLVMExternalLinkage);
        // LLVM.SetDLLStorageClass(main, LLVMDLLStorageClass.LLVMDLLExportStorageClass);
    }
}

public class FunctionBodyListener : arctBaseListener
{
    public LLVMModuleRef Module { get; set; }
    public FunctionAST Ast;
    public LLVMBuilderRef Builder;
    public LLVMValueRef? Functions;
    
    public FunctionBodyListener(LLVMBuilderRef builder,FunctionAST ast, LLVMModuleRef module,LLVMValueRef? functions)
    {
        Builder = builder;
        Ast = ast;
        Module = module;
        Functions = functions;
    }

    public override void EnterFunctionBody(arctParser.FunctionBodyContext context)
    {
        foreach (var stm in context.children)
        {
            StatementListener listener = new StatementListener(this.Builder, this.Ast, this.Module,Functions);
            ParseTreeWalker.Default.Walk(listener, stm);
        }
       
    }
}

public class StatementListener : arctBaseListener
{
    public LLVMModuleRef Module { get; set; }
    public FunctionAST Ast;
    public LLVMBuilderRef Builder;
    public LLVMValueRef? Conditions;
    public LLVMValueRef? Functions;
    public LLVMBasicBlockRef WhileBodyBlock; 
    public LLVMBasicBlockRef WhileAfterBlock;
    public LLVMBasicBlockRef IfCondHead;
    public LLVMBasicBlockRef WhileCondHead { get; set; }

    public StatementListener(LLVMBuilderRef builder, FunctionAST ast, LLVMModuleRef module,LLVMValueRef? functions)
    {
        Builder = builder;
        Ast = ast;
        Module = module;
        Functions = functions;
        Conditions = null;
    }

    public override void EnterWhileStatement(arctParser.WhileStatementContext context)
    {
        this.WhileCondHead = LLVM.AppendBasicBlock((LLVMValueRef)Functions, "while_cond");
        this.WhileBodyBlock = LLVM.AppendBasicBlock((LLVMValueRef)Functions, "while_body");
        this.WhileAfterBlock = LLVM.AppendBasicBlock((LLVMValueRef)Functions, "while_after");
        
        
        ExpressionListener listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context.equation().expression(0));
        var left = listener.stack.Pop();
        
        listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context.equation().expression(1));
        var right = listener.stack.Pop();
        string oper = context.equation().op.GetText();
        LLVMIntPredicate PredicateCmp = default;
        if (oper == ">")
        {
            PredicateCmp = LLVMIntPredicate.LLVMIntSGT;
        }
        else if (oper == "<")
        { 
            PredicateCmp = LLVMIntPredicate.LLVMIntSLT;
        }
        else if (oper == "==")
        {
            PredicateCmp = LLVMIntPredicate.LLVMIntEQ;
        }
        else if (oper == "!=")
        { 
            PredicateCmp = LLVMIntPredicate.LLVMIntNE;
        }

        LLVM.BuildBr(Builder, WhileCondHead);
        LLVM.PositionBuilderAtEnd(Builder, WhileCondHead);
        var CondHead = LLVM.BuildICmp(Builder, PredicateCmp, left, right, "icmp_check");
        LLVM.BuildCondBr(Builder, CondHead, WhileBodyBlock, WhileAfterBlock);
       

        LLVM.PositionBuilderAtEnd(Builder,WhileBodyBlock);
        
        

    }

   

    public override void ExitWhileStatement(arctParser.WhileStatementContext context)
    {
        ExpressionListener listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context.equation().expression(0));
        var left = listener.stack.Pop();
        
        listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context.equation().expression(1));
        var right = listener.stack.Pop();
        string oper = context.equation().op.GetText();
        LLVMIntPredicate PredicateCmp = default;
        if (oper == ">")
        {
            PredicateCmp = LLVMIntPredicate.LLVMIntSGT;
        }
        else if (oper == "<")
        { 
            PredicateCmp = LLVMIntPredicate.LLVMIntSLT;
        }
        else if (oper == "==")
        {
            PredicateCmp = LLVMIntPredicate.LLVMIntEQ;
        }
        else if (oper == "!=")
        { 
            PredicateCmp = LLVMIntPredicate.LLVMIntNE;
        }

        // LLVM.BuildBr(Builder, WhileCondHead);
        var CondHead = LLVM.BuildICmp(Builder, PredicateCmp, left, right, "icmp_check");
        LLVM.BuildCondBr(Builder, CondHead, WhileBodyBlock, WhileAfterBlock);
        // LLVM.BuildBr(Builder, WhileAfterBlock);
        LLVM.PositionBuilderAtEnd(Builder,WhileAfterBlock);
    }

    public override void EnterIfStatement(arctParser.IfStatementContext context)
    {
        ExpressionListener listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context.equation().expression(0));
        var left = listener.stack.Pop();
        
        listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context.equation().expression(1));
        var right = listener.stack.Pop();
        
        string oper = context.equation().op.GetText();
        LLVMIntPredicate PredicateCmp = default;
        if (oper == ">")
        {
            PredicateCmp = LLVMIntPredicate.LLVMIntSGT;
        }
        else if (oper == "<")
        { 
            PredicateCmp = LLVMIntPredicate.LLVMIntSLT;
        }
        else if (oper == "==")
        {
           PredicateCmp = LLVMIntPredicate.LLVMIntEQ;
        }
        else if (oper == "!=")
        { 
            PredicateCmp = LLVMIntPredicate.LLVMIntNE;
        }

        
        this.IfCondHead = LLVM.AppendBasicBlock((LLVMValueRef)Functions, "if_cond");
        var ifTrue = LLVM.AppendBasicBlock((LLVMValueRef)this.Functions, "ifTrue");
        var ifFalse = LLVM.AppendBasicBlock((LLVMValueRef)this.Functions, "ifFalse");
        var exit = LLVM.AppendBasicBlock((LLVMValueRef)this.Functions, "exit");
        LLVM.BuildBr(Builder,this.IfCondHead);
        LLVM.PositionBuilderAtEnd(Builder,this.IfCondHead);
        
        Conditions = LLVM.BuildICmp(Builder, PredicateCmp, left, right, "icmp_check_if");
        LLVM.BuildCondBr(Builder, (LLVMValueRef)Conditions, ifTrue,ifFalse);
        LLVM.PositionBuilderAtEnd(Builder, ifTrue);
    }

    public override void ExitIfStatement(arctParser.IfStatementContext context)
    {
        var exit = LLVM.GetLastBasicBlock((LLVMValueRef)Functions);
        var ifFalse = LLVM.GetPreviousBasicBlock(exit);
        // LLVM.BuildBr(Builder, ifFalse);
        LLVM.PositionBuilderAtEnd(Builder, ifFalse);
        LLVM.BuildBr(Builder, exit);
        LLVM.PositionBuilderAtEnd(Builder, exit);
    }


    public override void EnterPrintStatement(arctParser.PrintStatementContext context)
    {
        LLVMValueRef[] formatString = default;
        LLVMValueRef text = default;
        LLVMValueRef frm_str = default;
        
        
        var getPuts = LLVM.GetNamedFunction(Module,"printf");
        if (getPuts.ToString() == "Printing <null> Value")
        {
            LLVMTypeRef retType = LLVM.FunctionType(LLVMTypeRef.Int32Type(), new LLVMTypeRef[] {LLVMTypeRef.PointerType(LLVMTypeRef.Int8Type(), 0),  }, true);
            var puts = LLVM.AddFunction(Module,"printf",retType);
            getPuts = LLVM.GetNamedFunction(Module,"printf");
        }

        if (context.STRING() is { } str)
        {
            frm_str = LLVM.BuildGlobalStringPtr(Builder,"%s\n","fmp");
            text = LLVM.BuildGlobalStringPtr(Builder,str.GetText().Substring(1, str.GetText().Length - 2),"fmp");
        }
        else if (context.atom().INT() is { } intStr)
        {
            frm_str = LLVM.BuildGlobalStringPtr(Builder,"%s\n","fmp");
            text = LLVM.BuildGlobalStringPtr(Builder,intStr.GetText(),"fmp");
        }
        else if (context.atom().DECIMAL() is { } decStr)
        {
            frm_str = LLVM.BuildGlobalStringPtr(Builder,"%s\n","fmp");
            text = LLVM.BuildGlobalStringPtr(Builder,decStr.GetText(),"fmp");
            
        }
        else if (context.atom().ID() is { } id)
        {
            var pointer = Ast.GetVariable(context.atom().ID().GetText());
            if (pointer.IsAArgument().ToString() == "Printing <null> Value")
            {
                text = LLVM.BuildLoad(this.Builder, pointer, "print_"+context.atom().ID().GetText());
            }
            else
            { 
                text = pointer;
            }
            if (pointer.TypeOf().ToString() == "double*" || pointer.TypeOf().ToString() == "double")
            {
                frm_str = LLVM.BuildGlobalStringPtr(Builder,"%f\n","fmp");
            }
            else
            {
                frm_str = LLVM.BuildGlobalStringPtr(Builder,"%i\n","fmp");
              
            }
            
        }
        var putsArgs = new LLVMValueRef[] {frm_str, text };
        LLVM.BuildCall(Builder, getPuts, putsArgs, "print_tmp");
        
      


        
      

    }

    public override void EnterMoveValueVariable(arctParser.MoveValueVariableContext context)
    {
        
            string name = context.ID().GetText();
            ExpressionListener listener = new ExpressionListener(this.Builder,this.Ast);
            ParseTreeWalker.Default.Walk(listener, context);
        
            LLVMValueRef value = listener.stack.Pop();
            var ptrValue = this.Ast.GetVariable(name);
            LLVM.BuildStore(this.Builder, value, ptrValue);
            this.Ast.AddVariable(name,ptrValue);
            
       
    }

    public override void EnterReturnStatement(arctParser.ReturnStatementContext context)
    {
        ExpressionListener listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context);

      
        LLVM.BuildRet(Builder,listener.stack.Pop());
        
        
    }

    public override void EnterAssignmentStatement(arctParser.AssignmentStatementContext context)
    {
        string name = context.ID().GetText();
        var type = context.type().GetText() == "int" ? LLVMTypeRef.Int32Type() : LLVMTypeRef.DoubleType();
        
        ExpressionListener listener = new ExpressionListener(this.Builder,this.Ast);
        ParseTreeWalker.Default.Walk(listener, context);
        
        var ptrValue = LLVM.BuildAlloca(this.Builder, type, name);
        LLVMValueRef value = listener.stack.Pop();
        
        LLVM.BuildStore(this.Builder, value,ptrValue);
        Ast?.AddVariable(name,ptrValue);

    }
}

public class ExpressionListener : arctBaseListener


{
    public Stack<LLVMValueRef> stack = new Stack<LLVMValueRef>();
    public FunctionAST Ast;
    public LLVMBuilderRef Builder;

    public ExpressionListener(LLVMBuilderRef builder, FunctionAST ast)
    {
        Builder = builder;
        Ast = ast;
    }

    public override void ExitExpressionFunctionCall(arctParser.ExpressionFunctionCallContext context)
    {
        var call_function_name = Ast.MainAST.get_function(context.functionCall().ID().GetText());
        var call_function = call_function_name.Function;
        
        var parametrs = new List<LLVMValueRef>();

        foreach (var VARIABLE in context.functionCall().@params().param())
        {
            parametrs.Add(stack.Pop());
        }

        parametrs.Reverse();
        LLVMValueRef[] array_param = parametrs.ToArray();
        stack.Push(LLVM.BuildCall(Builder,(LLVMValueRef)call_function,array_param,context.functionCall().ID().GetText()+"_temp"));

    }

    public override void ExitExpressionMul(arctParser.ExpressionMulContext context)
    {
        var right = stack.Pop();
        var left = stack.Pop();
        
        right = LLVM.BuildSIToFP(Builder, right, LLVMTypeRef.DoubleType(),"tmp_val_r");
        left = LLVM.BuildSIToFP(Builder, left, LLVMTypeRef.DoubleType(),"tmp_val_t");

        if (context.op.Text == "*")
        {
            var result = LLVM.BuildFMul(Builder,left, right,"tmp_fmul_v");
            stack.Push(result);
        }
        else if (context.op.Text == "/")
        {
                
            var result = LLVM.BuildFDiv(Builder,left, right,"tmp_fdiv_v");
            stack.Push(result);
        }
        else if (context.op.Text == "%")
        {
            var result = LLVM.BuildFRem(Builder,left, right,"tmp_frem_v");
            stack.Push(result);
        }
        
        
    }

    public override void ExitExpressionAdd(arctParser.ExpressionAddContext context)
    {   
        var right = stack.Pop();
        var left = stack.Pop();
        LLVMValueRef result;
        if (left.TypeOf().ToString() == "double" || right.TypeOf().ToString() == "double" )
        {
            right = LLVM.BuildSIToFP(Builder, right, LLVMTypeRef.DoubleType(),"tmp_val_r");
            left = LLVM.BuildSIToFP(Builder, left, LLVMTypeRef.DoubleType(),"tmp_val_t");
            result = context.op.Text == "+"
                ? LLVM.BuildFAdd(this.Builder, left, right, "temp_faad")
                : LLVM.BuildFSub(this.Builder, left, right, "temp_fsub"); 
               
            stack.Push(result);
        }
        else
        {
            result = context.op.Text == "+"
                ? LLVM.BuildAdd(this.Builder, left, right, "tmp_add")
                : LLVM.BuildSub(this.Builder, left, right, "tmp_add");
            stack.Push(result);
        }

    }

    public override void ExitConvert_type(arctParser.Convert_typeContext context)
    {
        LLVMValueRef expr = stack.Pop();
        expr = context.type().GetText() == "double" ? LLVM.ConstSIToFP(expr, LLVMTypeRef.DoubleType()) : LLVM.ConstFPToSI(expr, LLVMTypeRef.Int32Type());
        stack.Push(expr);
    }

    public override void EnterAtom(arctParser.AtomContext context)
    {
        LLVMValueRef result = default;
        if (context.INT() is { } i)
        {
            result = LLVM.ConstInt(LLVM.Int32Type(), (ulong)int.Parse(i.GetText()), false);
        }
        else if (context.DECIMAL() is { } d)
            result = LLVM.ConstReal(LLVM.DoubleType(), double.Parse(d.GetText()));
        else if (context.ID() is { } id)
        {
            var pointer = Ast.GetVariable(context.GetText());
            if (pointer.IsAArgument().ToString() == "Printing <null> Value")
            {
                result = LLVM.BuildLoad(this.Builder, pointer, context.GetText());
            }
            else
            { 
                result = pointer;
            }
            
            
        }
        else
        {
            Console.WriteLine("Ты лох ебаный");
        }
        stack.Push(result);
            
    }
}




// public class OverrideListener : arctBaseListener
// {
//     public LLVMModuleRef mod { get; set; }
//     public LLVMBuilderRef builder;
//     public LLVMValueRef main;
//     public override void EnterProgram([NotNull] arctParser.ProgramContext context)
//     {
//         mod = LLVM.ModuleCreateWithName("LLVMSharpIntro");
//     }
//     public override void EnterMain([NotNull] arctParser.MainContext context)
//     {
         // LLVMTypeRef[] param_types = {LLVM.Int32Type(),LLVM.Int32Type()};
         // LLVMTypeRef ret_type = LLVM.FunctionType(LLVM.Int32Type(), param_types, false);
         // main = LLVM.AddFunction(mod, "main", ret_type);
         // LLVMBasicBlockRef entry = LLVM.AppendBasicBlock(main, "entry");
         //
         // builder = LLVM.CreateBuilder();
         // LLVM.PositionBuilderAtEnd(builder, entry);
//     }
//     public override void ExitMain([NotNull] arctParser.MainContext context)
//     {
         // LLVMValueRef tmp = LLVM.BuildAdd(builder, LLVM.GetParam(main, 0), LLVM.GetParam(main, 1), "tmp");
         // LLVM.BuildRet(builder, tmp);
         // LLVM.SetLinkage(main, LLVMLinkage.LLVMExternalLinkage);
         // LLVM.SetDLLStorageClass(main, LLVMDLLStorageClass.LLVMDLLExportStorageClass);
//     }
//
//     public override void ExitProgram([NotNull] arctParser.ProgramContext context)
//     {
//         ModuleClass.Module = this.mod;
//         ModuleClass.main = this.main;
//     }
//     public override void EnterVariable([NotNull] arctParser.VariableContext context)
//     {
//         var name = context.identifier().GetText();
//         var type =  LLVMTypeRef.Int32Type();
//         if (context.type().GetText() == "double")
//         {
//             type = LLVMTypeRef.DoubleType();
//         }
//         var ptrValue = LLVM.BuildAlloca(builder,type,name);
//
//         if (context.EQ() is { } i)
//         {
//             IarctListener printer = new ExpressionListener(builder);
//             ParseTreeWalker.Default.Walk(printer, context);
//             LLVMValueRef value = ModuleClass.stack.Pop();
//             LLVM.BuildStore(builder, value,ptrValue );
//             if (ModuleClass.main_variable.ContainsKey(name) == true)
//             {
//                 ModuleClass.main_variable.Remove(name);
//                 ModuleClass.main_variable.Add(name, value);
//             }
//             else
//             {
//                 ModuleClass.main_variable.Add(name,value);
//             }
//         }
//         else
//         {
//             Console.WriteLine("Initialize function is coming soon");
//         }
//
//
//
//
//
//
//         //LLVMValueRef variable = LLVM.BuildAlloca(builder, LLVM.Int32Type(), "myVariable");
//         //
//
//     }
//
// }