using System.Net.Mail;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LLVMSharp;

namespace ArctCompiler;

public class MainAST
{
    private static Dictionary<string, FunctionAST> functionList = new Dictionary<string, FunctionAST>();

    public void Add_Function(string name,FunctionAST pointer)
    {
        functionList.Add(name, pointer);
    }
    private static FunctionAST? get_function(string name)
    {
        if (functionList.TryGetValue(name, out FunctionAST value))
        {
            return value;
        }
        else
        {
            return null;
        }
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
    LLVMValueRef main;
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

    public override void EnterFunctionBody(arctParser.FunctionBodyContext context)
    {
        LLVMTypeRef[] paramTypes = {LLVM.Int32Type(),LLVM.Int32Type()};
        LLVMTypeRef ret_type = LLVM.FunctionType(LLVM.Int32Type(), paramTypes, false);
        main = LLVM.AddFunction(Module, "main", ret_type);
        LLVMBasicBlockRef entry = LLVM.AppendBasicBlock(main, "entry");
        this.Builder = LLVM.CreateBuilder();
        LLVM.PositionBuilderAtEnd(Builder, entry);
        Ast = new FunctionAST("main", MainAst, main);
        
        FunctionBodyListener listener = new FunctionBodyListener(this.Builder, this.Ast, this.Module);
        ParseTreeWalker.Default.Walk(listener, context);
        
    }

    public override void ExitFunctionBody(arctParser.FunctionBodyContext context)
    {
        LLVMValueRef tmp = LLVM.BuildAdd(Builder, LLVM.GetParam(main, 0), LLVM.GetParam(main, 1), "tmp");
        LLVM.BuildRet(Builder, tmp);
        LLVM.SetLinkage(main, LLVMLinkage.LLVMExternalLinkage);
        LLVM.SetDLLStorageClass(main, LLVMDLLStorageClass.LLVMDLLExportStorageClass);
    }
}

public class FunctionBodyListener : arctBaseListener
{
    public LLVMModuleRef Module { get; set; }
    public FunctionAST Ast;
    public LLVMBuilderRef Builder;
    
    public FunctionBodyListener(LLVMBuilderRef builder,FunctionAST ast, LLVMModuleRef module)
    {
        Builder = builder;
        Ast = ast;
        Module = module;
    }

    public override void EnterFunctionBody(arctParser.FunctionBodyContext context)
    {
        foreach (var stm in context.children)
        {
            StatementListener listener = new StatementListener(this.Builder, this.Ast, this.Module);
            ParseTreeWalker.Default.Walk(listener, stm);
        }
       
    }
}

public class StatementListener : arctBaseListener
{
    public LLVMModuleRef Module { get; set; }
    public FunctionAST Ast;
    public LLVMBuilderRef Builder;
    public LLVMValueRef Conditions;

    public StatementListener(LLVMBuilderRef builder, FunctionAST ast, LLVMModuleRef module)
    {
        Builder = builder;
        Ast = ast;
        Module = module;
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
            result = LLVM.ConstReal(LLVM.DoubleType(), (ulong)double.Parse(d.GetText()));
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