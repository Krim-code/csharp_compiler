using System.Net.Mail;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LLVMSharp;

namespace ArctCompiler;

public static class ModuleClass
{
    public static LLVMModuleRef Module { get; set; }
    public static LLVMValueRef main { get; set; }
    public static Stack<LLVMValueRef> stack = new Stack<LLVMValueRef>();

    public static Dictionary<string, LLVMValueRef> main_variable = new Dictionary<string, LLVMValueRef>();
}

public class ExpressionListener : arctBaseListener
{
    public LLVMBuilderRef builder;

    public ExpressionListener(LLVMBuilderRef b) { builder = b;}

   

    public override void ExitExpressionAdd(arctParser.ExpressionAddContext context)
    {   
        var right = ModuleClass.stack.Pop();
        var left = ModuleClass.stack.Pop();
        LLVMValueRef result;
        if (left.TypeOf().ToString() == "double" || right.TypeOf().ToString() == "double" )
        {
            right = LLVM.BuildSIToFP(builder, right, LLVMTypeRef.DoubleType(),"tmp_val_r");
            left = LLVM.BuildSIToFP(builder, left, LLVMTypeRef.DoubleType(),"tmp_val_t");
            result = context.plusminus().GetText() == "+" ?
                LLVM.ConstFAdd(left, right) : 
                LLVM.ConstFSub(left, right);
            ModuleClass.stack.Push(result);
        }
        else
        {
            result = context.plusminus().GetText() == "+" ? 
                LLVM.ConstAdd( left, right) : 
                LLVM.ConstSub(left, right);
            ModuleClass.stack.Push(result);
        }

    }
    
    public override void ExitExpressionMul(arctParser.ExpressionMulContext context)
    {
        var right = ModuleClass.stack.Pop();
        var left = ModuleClass.stack.Pop();
        
        right = LLVM.BuildSIToFP(builder, right, LLVMTypeRef.DoubleType(),"tmp_val_r");
        left = LLVM.BuildSIToFP(builder, left, LLVMTypeRef.DoubleType(),"tmp_val_t");

            if (context.multdivmod().GetText() == "*")
            {
                var result = LLVM.ConstFMul(left, right);
                ModuleClass.stack.Push(result);
            }
            else if (context.multdivmod().GetText() == "/")
            {
                
                var result = LLVM.ConstFDiv(left, right);
                ModuleClass.stack.Push(result);
            }
            else if (context.multdivmod().GetText() == "%")
            {
                var result = LLVM.ConstFRem(left, right);
                ModuleClass.stack.Push(result);
            }
        
        
    }

    public override void ExitExpressionConvert(arctParser.ExpressionConvertContext context)
    {
        LLVMValueRef expr = ModuleClass.stack.Pop();
        expr = context.type().GetText() == "double" ? LLVM.ConstSIToFP(expr, LLVMTypeRef.DoubleType()) : LLVM.ConstFPToSI(expr, LLVMTypeRef.Int32Type());
        ModuleClass.stack.Push(expr);
    }


    public override void EnterFactor([NotNull] arctParser.FactorContext context)
    {

        if (context.INTEGER() is { } i)
        {
            ModuleClass.stack.Push(LLVM.ConstInt(LLVM.Int32Type(), (ulong)int.Parse(i.GetText()), false));

        }
        if (context.DOUBLE() is { } d)
        {
            ModuleClass.stack.Push(LLVM.ConstReal(LLVM.DoubleType(), (ulong)Double.Parse(d.GetText())));
        }


        if (context.identifier() is { } id)
        {
            try
            {
                var ptr = ModuleClass.main_variable
                    .Where(x => x.Key == id.GetText())
                    .ElementAt(0);
                ModuleClass.stack.Push(ptr.Value);
            }
            catch (Exception e)
            {
                Console.WriteLine("Ты лох ебаный");
                throw;
            }
        }
            
       
        
    }
}
public class OverrideListener : arctBaseListener
{
    public LLVMModuleRef mod { get; set; }
    public LLVMBuilderRef builder;
    public LLVMValueRef main;
    public override void EnterProgram([NotNull] arctParser.ProgramContext context)
    {
        mod = LLVM.ModuleCreateWithName("LLVMSharpIntro");
    }
    public override void EnterMain([NotNull] arctParser.MainContext context)
    {
        LLVMTypeRef[] param_types = {LLVM.Int32Type(),LLVM.Int32Type()};
        LLVMTypeRef ret_type = LLVM.FunctionType(LLVM.Int32Type(), param_types, false);
        main = LLVM.AddFunction(mod, "main", ret_type);
        LLVMBasicBlockRef entry = LLVM.AppendBasicBlock(main, "entry");

        builder = LLVM.CreateBuilder();
        LLVM.PositionBuilderAtEnd(builder, entry);
    }
    public override void ExitMain([NotNull] arctParser.MainContext context)
    {
        LLVMValueRef tmp = LLVM.BuildAdd(builder, LLVM.GetParam(main, 0), LLVM.GetParam(main, 1), "tmp");
        LLVM.BuildRet(builder, tmp);
        LLVM.SetLinkage(main, LLVMLinkage.LLVMExternalLinkage);
        LLVM.SetDLLStorageClass(main, LLVMDLLStorageClass.LLVMDLLExportStorageClass);
    }

    public override void ExitProgram([NotNull] arctParser.ProgramContext context)
    {
        ModuleClass.Module = this.mod;
        ModuleClass.main = this.main;
    }
    public override void EnterVariable([NotNull] arctParser.VariableContext context)
    {
        var name = context.identifier().GetText();
        var type =  LLVMTypeRef.Int32Type();
        if (context.type().GetText() == "double")
        {
            type = LLVMTypeRef.DoubleType();
        }
        var ptrValue = LLVM.BuildAlloca(builder,type,name);

        if (context.EQ() is { } i)
        {
            IarctListener printer = new ExpressionListener(builder);
            ParseTreeWalker.Default.Walk(printer, context);
            LLVMValueRef value = ModuleClass.stack.Pop();
            LLVM.BuildStore(builder, value,ptrValue );
            if (ModuleClass.main_variable.ContainsKey(name) == true)
            {
                ModuleClass.main_variable.Remove(name);
                ModuleClass.main_variable.Add(name, value);
            }
            else
            {
                ModuleClass.main_variable.Add(name,value);
            }
        }
        else
        {
            Console.WriteLine("Initialize function is coming soon");
        }






        //LLVMValueRef variable = LLVM.BuildAlloca(builder, LLVM.Int32Type(), "myVariable");
        //

    }

}