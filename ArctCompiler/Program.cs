using System.ComponentModel;
using System.Diagnostics;
using LLVMSharp;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace ArctCompiler
{
    class ArctCompiler
    {
        class Arct
        {
            public unsafe Arct()
            {
                
                String input = File.ReadAllText(@"C:\Users\Redmi\Documents\Programming\C#\CourseCompiler\ArctCompiler\paste.txt");
                ICharStream stream = CharStreams.fromString(input);
                ITokenSource lexer = new arctLexer(stream);
                ITokenStream tokens = new CommonTokenStream(lexer);
                arctParser parser = new arctParser(tokens);
                parser.BuildParseTree = true;
                IParseTree tree = parser.program();

                OverrideListener printer = new OverrideListener();
                ParseTreeWalker.Default.Walk(printer, tree);
                LLVMModuleRef mod = printer.Module;
           
                LLVMBool Success = new LLVMBool(0);
                
                if (LLVM.VerifyModule(mod, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != Success)
                {
                    Console.WriteLine($"Error: {error}");
                }

                LLVM.InitializeX86TargetMC();
                LLVM.InitializeX86Target();
                LLVM.InitializeX86TargetInfo();
                LLVM.InitializeX86AsmParser();
                LLVM.InitializeX86AsmPrinter();

              
                LLVM.DumpModule(mod);
                if (LLVM.GetTargetFromTriple("x86_64-pc-win32", out var target, out error) == Success)
                {
                    var targetMachine = LLVM.CreateTargetMachine(target, "x86_64-pc-windows-msvc", "generic", "",
                        LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault,
                        LLVMCodeModel.LLVMCodeModelDefault);
                    var dl = LLVM.CreateTargetDataLayout(targetMachine);
                    LLVM.SetModuleDataLayout(mod, dl);
                    LLVM.SetTarget(mod, "x86_64-pc-win32");
                    byte[] buffer = System.Text.Encoding.Default.GetBytes("test.o\0");

                    fixed (byte* ptr = buffer)
                    {
                        LLVM.TargetMachineEmitToFile(targetMachine, mod, new IntPtr(ptr),
                            LLVMCodeGenFileType.LLVMObjectFile, out error);
                    }
                }
            }
        }

        class acrtLLVM
        {
            public unsafe acrtLLVM()
            {
                LLVMBool Success = new LLVMBool(0);
                LLVMModuleRef mod = LLVM.ModuleCreateWithName("LLVMSharpIntro");

                LLVMTypeRef[] param_types = { LLVM.Int32Type(), LLVM.Int32Type() };
                LLVMTypeRef ret_type = LLVM.FunctionType(LLVM.Int32Type(), param_types, false);
                LLVMValueRef sum = LLVM.AddFunction(mod, "main", ret_type);

                LLVMBasicBlockRef entry = LLVM.AppendBasicBlock(sum, "entry");

                LLVMBuilderRef builder = LLVM.CreateBuilder();
                LLVM.PositionBuilderAtEnd(builder, entry);
                LLVMValueRef tmp = LLVM.BuildAdd(builder, LLVM.GetParam(sum, 0), LLVM.GetParam(sum, 1), "tmp");
                LLVM.BuildRet(builder, tmp);
                LLVM.SetLinkage(sum, LLVMLinkage.LLVMExternalLinkage);
                LLVM.SetDLLStorageClass(sum, LLVMDLLStorageClass.LLVMDLLExportStorageClass);

                if (LLVM.VerifyModule(mod, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != Success)
                {
                    Console.WriteLine($"Error: {error}");
                }

                LLVM.InitializeX86TargetMC();
                LLVM.InitializeX86Target();
                LLVM.InitializeX86TargetInfo();
                LLVM.InitializeX86AsmParser();
                LLVM.InitializeX86AsmPrinter();

                var aa = LLVM.GetLinkage(sum);
                LLVM.DumpModule(mod);
                if (LLVM.GetTargetFromTriple("x86_64-pc-win32", out var target, out error) == Success)
                {
                    var targetMachine = LLVM.CreateTargetMachine(target, "x86_64-pc-windows-msvc", "generic", "",
                        LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault,
                        LLVMCodeModel.LLVMCodeModelDefault);
                    var dl = LLVM.CreateTargetDataLayout(targetMachine);
                    LLVM.SetModuleDataLayout(mod, dl);
                    LLVM.SetTarget(mod, "x86_64-pc-win32");
                    byte[] buffer = System.Text.Encoding.Default.GetBytes("test.o\0");

                    fixed (byte* ptr = buffer)
                    {
                        LLVM.TargetMachineEmitToFile(targetMachine, mod, new IntPtr(ptr),
                            LLVMCodeGenFileType.LLVMObjectFile, out error);
                    }
                }

                Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c " + "clang test.o -o hello.exe ";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                // Start the process and read the output
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Write the output to the console
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c " + "hello";
                process.Start();
                Console.WriteLine(output);
            }
        }

    public static void Main(string[] args)
    {
        Arct comp = new Arct();
        //acrtLLVM arctLlvm = new acrtLLVM(); 
    }
}

}