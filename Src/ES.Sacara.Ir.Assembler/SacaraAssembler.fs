﻿namespace ES.Sacara.Ir.Assembler

open System
open System.Reflection
open System.Text.RegularExpressions
open System.Collections.Generic
open ES.Sacara.Ir.Parser
open ES.Sacara.Ir.Parser.IrAst
open ES.Sacara.Ir.Core
open ES.Sacara.Ir.Obfuscator
open System.IO

type IrAssemblyCode = {
    Functions: VmFunction list
    Warnings: String list
} with
    member this.GetBuffer() =
        this.Functions
        |> List.map(fun vmFunction -> vmFunction.Body)
        |> List.concat
        |> List.map(fun vmOpCode -> vmOpCode.Buffer)
        |> Array.concat

    override this.ToString() =
        this.Functions
        |> List.map(fun vmFunction -> vmFunction.Body)
        |> List.concat
        |> List.map(string)
        |> fun l -> String.Join(Environment.NewLine, l)

type SacaraAssembler(settings: AssemblerSettings) =
    let mutable _currentFunction = new IrFunction(String.Empty)
    let mutable _functions = new List<IrFunction>()
    let mutable _currentLabel: String option = None
    let mutable _currentIp = 0    

    let addOperation(opCode: IrOpCode) =
        if _currentLabel.IsSome then
            opCode.Label <- _currentLabel
            _currentLabel <- None
        _currentFunction.Body.Add(opCode)
        
    let rec parseOperationExpression(expression: Expression) =
        match expression with
        | Number num -> 
            new Operand(num.Value) |> Some
        | Identifier identifier -> 
            new Operand(identifier.Name) |> Some
        | StatementExpression statement ->
            parseStatement(statement)
            None 

    and parseStatement(statement: Statement) =
        match statement with
        | Statement.Procedure procType ->            
            _currentFunction <- new IrFunction(procType.Name)
            _functions.Add(_currentFunction)
            procType.Body |> List.iter(parseStatement)
        | Statement.Push pushType ->
            parseOperationExpression(pushType.Operand) 
            |> Option.iter(fun op ->
                let push = new IrOpCode(IrInstruction.Push, settings.UseMultipleOpcodeForSameInstruction)
                push.Operands.Add(op)
                addOperation(push)
            )
        | Statement.Pop popType ->
            let pop = new IrOpCode(IrInstruction.Pop, settings.UseMultipleOpcodeForSameInstruction)
            pop.Operands.Add(new Operand(popType.Identifier))
            addOperation(pop)
        | Statement.Label labelType -> 
            _currentLabel <- Some labelType.Name
            parseStatement(labelType.Statement)
        | Statement.Call callType -> 
            addOperation(new IrOpCode((if callType.Native then IrInstruction.NativeCall else IrInstruction.Call), settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Read readType ->
            addOperation(new IrOpCode((if readType.Native then IrInstruction.NativeRead else IrInstruction.Read), settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Write writeType ->
            addOperation(new IrOpCode((if writeType.Native then IrInstruction.NativeWrite else IrInstruction.Write), settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Nop -> 
            addOperation(new IrOpCode(IrInstruction.Nop, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.GetIp ->
            addOperation(new IrOpCode(IrInstruction.GetIp, settings.UseMultipleOpcodeForSameInstruction))        
        | Statement.Add ->
            addOperation(new IrOpCode(IrInstruction.Add, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Ret ->
            addOperation(new IrOpCode(IrInstruction.Ret, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.JumpIf jumpIfType -> 
            let opCode =
                match (jumpIfType.JumpIfEquals, jumpIfType.JumpIfLess) with
                | (true, true) -> IrInstruction.JumpIfLessEquals
                | (true, false) -> IrInstruction.JumpIfGreaterEquals
                | (false, true) -> IrInstruction.JumpIfLess
                | (false, false) -> IrInstruction.JumpIfGreater

            addOperation(new IrOpCode(opCode, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Jump ->
            addOperation(new IrOpCode(IrInstruction.Jump, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Empty -> ()
        | Statement.Alloca ->
            addOperation(new IrOpCode(IrInstruction.Alloca, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Byte b ->
            let byte = new IrOpCode(IrInstruction.Byte, settings.UseMultipleOpcodeForSameInstruction)
            byte.Operands.Add(new Operand(b))
            addOperation(byte)
        | Statement.Word w ->
            let word = new IrOpCode(IrInstruction.Word, settings.UseMultipleOpcodeForSameInstruction)
            word.Operands.Add(new Operand(w))
            addOperation(word)
        | Statement.DoubleWord dw ->
            let dword = new IrOpCode(IrInstruction.DoubleWord, settings.UseMultipleOpcodeForSameInstruction)
            dword.Operands.Add(new Operand(dw))
            addOperation(dword)
        | Statement.Halt ->
            addOperation(new IrOpCode(IrInstruction.Halt, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Cmp ->
            addOperation(new IrOpCode(IrInstruction.Cmp, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.GetSp ->
            addOperation(new IrOpCode(IrInstruction.GetSp, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.StackRead ->
            addOperation(new IrOpCode(IrInstruction.StackRead, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.StackWrite ->
            addOperation(new IrOpCode(IrInstruction.StackWrite, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Sub ->
            addOperation(new IrOpCode(IrInstruction.Sub, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Mul ->
            addOperation(new IrOpCode(IrInstruction.Mul, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Div ->
            addOperation(new IrOpCode(IrInstruction.Div, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.And ->
            addOperation(new IrOpCode(IrInstruction.And, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Or ->
            addOperation(new IrOpCode(IrInstruction.Or, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Not ->
            addOperation(new IrOpCode(IrInstruction.Not, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Xor ->
            addOperation(new IrOpCode(IrInstruction.Xor, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Nor ->
            addOperation(new IrOpCode(IrInstruction.Nor, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.ShiftLeft ->
            addOperation(new IrOpCode(IrInstruction.ShiftLeft, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.ShiftRight ->
            addOperation(new IrOpCode(IrInstruction.ShiftRight, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.SetIp ->
            addOperation(new IrOpCode(IrInstruction.SetIp, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.SetSp ->
            addOperation(new IrOpCode(IrInstruction.SetSp, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Inc ->
            addOperation(new IrOpCode(IrInstruction.Inc, settings.UseMultipleOpcodeForSameInstruction))
        | Statement.Block statementList ->
            statementList |> Seq.iter(parseStatement)
        | Statement.IncludeFile fileName ->
            includeFile(fileName)
        | Statement.Mod ->
            addOperation(new IrOpCode(IrInstruction.Mod, settings.UseMultipleOpcodeForSameInstruction))

    and includeFile(rawFileName: String) =
        let fileName = rawFileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)
        let fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), fileName)
        if not <| File.Exists(fullPath) then
            failwith(String.Format("Filename to include '{0}' not found.", fullPath))

        let irCode = File.ReadAllText(fullPath)
        parseCode(irCode)

    and parseAst(ast: Program) =
        match ast with
        | Program sl -> sl |> List.iter(parseStatement)  
        
    and parseCode(irCode: String) =
        let astBuilder = new SacaraAstBuilder()
        let ast = astBuilder.Parse(irCode)
        parseAst(ast)

    let assemblyIrOpCode(symbolTable: SymbolTable) (opCode: IrOpCode) =
        if opCode.Label.IsSome then
            symbolTable.AddLabel(opCode.Label.Value, _currentIp)

        let vmOpCode = opCode.Assemble(_currentIp, symbolTable)
        _currentIp <- _currentIp + vmOpCode.Buffer.Length
        vmOpCode

    let obfuscate(vmFunctions: VmFunction list, settings: AssemblerSettings) =
        vmFunctions
        |> List.iter(fun irFunction ->
            irFunction.Body
            |> List.iter(fun vmOpCode ->
                // encrypt the opcode if necessary
                if settings.RandomlyEncryptOpCode then  
                    Engines.encryptVmOpCode(vmOpCode)          
            
                // encrypt operands if necessary
                if settings.EncryptOperands then
                    Engines.encryptVmOperands(vmOpCode)
            )
        )

    let assemblyFunctionBody(irFunctionBody: IrOpCode seq, symbolTable: SymbolTable, settings: AssemblerSettings) =
        irFunctionBody
        |> Seq.map(assemblyIrOpCode(symbolTable))
        |> Seq.toList
        
    let addAllocaInstruction(symbolTable: SymbolTable, opCodes: IrOpCode list) =
        let allVariables = new HashSet<String>()

        // extract all local variables
        let opCodeAcceptingVariables = [
            IrInstruction.Push
            IrInstruction.Pop
        ]
        opCodes
        |> Seq.filter(fun opCode -> opCodeAcceptingVariables |> List.contains opCode.Type)
        |> Seq.iter(fun opCode -> 
            opCode.Operands
            |> Seq.iter(fun operand ->
                match operand.Value with
                | :? String ->
                    if not(symbolTable.IsLabel(operand.Value.ToString())) then
                        allVariables.Add(operand.Value.ToString()) |> ignore
                | _ -> ()
            )
        )
        
        // create alloca instruction
        if allVariables.Count > 0 then
            let pushInstr = new IrOpCode(IrInstruction.Push, settings.UseMultipleOpcodeForSameInstruction)
            pushInstr.Operands.Add(new Operand(allVariables.Count))

            let allocaInstr = new IrOpCode(IrInstruction.Alloca, settings.UseMultipleOpcodeForSameInstruction)
            [pushInstr;allocaInstr]@opCodes
        else    
            opCodes

    let parseIdentifierName(rawName: Object) =
        let rawName = rawName.ToString()
        let m = Regex.Match(rawName.ToString(), "^([0-9]+)#(.+)")
        if m.Success then
            let index = Int32.Parse(m.Groups.[1].Value)
            let name = m.Groups.[2].Value
            (name, Some index)
        else
            (rawName, None)

    let getLocalVariables(irFunction: IrFunction, symbolTable: SymbolTable) =
        irFunction.Body
        |> Seq.collect(fun irCode -> irCode.Operands)
        |> Seq.filter(fun operand -> 
            operand.IsIdentifier && not(symbolTable.IsLabel(operand.Value.ToString()))
        )       
        |> Seq.distinctBy(fun op -> op.Value.ToString())
        |> Seq.toList

    let sortAllVariables(localVariables: Operand list, sortedIndexedVariables: IDictionary<String, Operand>) =
        let mutable indexedOffset = 0
        let keys = sortedIndexedVariables.Keys |> Seq.toList
        let returnedOperands = new Dictionary<String, Operand>()

        localVariables
        |> Seq.toList
        |> List.map(fun op -> (op, parseIdentifierName(op)))
        |> List.map(fun (op, (name, _)) ->
            match sortedIndexedVariables.TryGetValue(name) with
            | (true, _) -> 
                match returnedOperands.TryGetValue(name) with
                | (true, operand) -> operand
                | _ ->
                    let key = keys.[indexedOffset]
                    indexedOffset <- indexedOffset + 1
                    let operand = sortedIndexedVariables.[key]
                    returnedOperands.[name] <- operand
                    operand
            | _ -> 
                op
        )
        |> List.distinctBy(fun op -> op.Value)

    let sortIndexedVariables(localVariables: Operand list) =
        localVariables
        |> List.map(fun op -> (op, parseIdentifierName(op)))
        |> List.filter(fun (_, (_, index)) -> index.IsSome)
        |> List.sortBy(fun (_, (_, index)) -> index.Value) 
        |> List.map(fun (op, (name, _)) -> (name, op))
        |> dict

    let rewriteLocalVariableOffset(sortedVariables: Operand list, irFunction: IrFunction) =
        irFunction.Body
        |> Seq.collect(fun irOpcode -> irOpcode.Operands)
        |> Seq.iter(fun op ->   
            let (name, _) = parseIdentifierName(op)
            sortedVariables
            |> List.iteri(fun index sop ->
                let (sname, _) = parseIdentifierName(sop)
                if name.Equals(sname, StringComparison.Ordinal) then
                    let offsetName = String.Format("{0}#{1}", index, sname)
                    op.Value <- offsetName
            )            
        )
        
    let generateFunctionVmOpCodes(symbolTable: SymbolTable, settings: AssemblerSettings) (irFunction: IrFunction) =
        symbolTable.StartFunction()
        
        // the analyzed function is a symbol, this will ensure that instruction like call foo, will be correctly assembled
        symbolTable.AddLabel(irFunction.Name, _currentIp)

        // correctly set offset for local variables
        let localVariables = getLocalVariables(irFunction, symbolTable)
        let sortedIndexedVariables = sortIndexedVariables(localVariables)
        let sortedVariables = sortAllVariables(localVariables, sortedIndexedVariables)
        rewriteLocalVariableOffset(sortedVariables, irFunction)

        let rawBody =
            if settings.UseNorOperator
            then Engines.reWriteInstructionWithNorOperator(irFunction.Body, settings.UseMultipleOpcodeForSameInstruction) |> Seq.toList
            else irFunction.Body |> Seq.toList

        // add alloca instruction to compute space for local variables
        let fullBody = addAllocaInstruction(symbolTable, rawBody)
                            
        // proceed to assemble VM opcodes        
        {Body=assemblyFunctionBody(fullBody, symbolTable, settings)}

    let orderFunctions(functions: List<IrFunction>, settings: AssemblerSettings) =        
        let entryPointFunction = functions |> Seq.find(fun f -> f.IsEntryPoint())
        
        let otherFunctions =
            if settings.ReorderFunctions then   
                let rnd = new Random()
                functions 
                |> Seq.filter(fun f -> not(f.Name.Equals("main", StringComparison.OrdinalIgnoreCase)))
                |> Seq.sortBy(fun _ -> rnd.Next())
                |> Seq.toList
            else
                functions 
                |> Seq.filter(fun f -> not(f.Name.Equals("main", StringComparison.OrdinalIgnoreCase)))                
                |> Seq.toList

        entryPointFunction::otherFunctions

    let addLabelNamesToSymbolTable(symbolTable: SymbolTable, functions: List<IrFunction>) =        
        functions
        |> Seq.iter(fun irFunction -> 
            symbolTable.AddLabelName(irFunction.Name)
            irFunction.Body
            |> Seq.filter(fun irOpCode -> irOpCode.Label.IsSome)
            |> Seq.iter(fun irOpCode -> symbolTable.AddLabelName(irOpCode.Label.Value))
        )

    let hasReturnOrHalt(irFun: IrFunction) =
        if irFun.Body |> Seq.exists(fun opCode -> [IrInstruction.Ret; IrInstruction.Halt] |> List.contains opCode.Type)
        then List.empty
        else [(String.Format("Function '{0}' doesn't contains a RET or HALT instruction, this may cause an infinite loop", irFun.Name))]

    let checkFunctionForWarnings(warnings: String list) (irFunction: IrFunction) =
        hasReturnOrHalt(irFunction)@
        warnings

    let checkForWarnings(functions: List<IrFunction>) =        
        functions
        |> Seq.toList        
        |> List.fold(checkFunctionForWarnings) List.empty

    new() = new SacaraAssembler(new AssemblerSettings())

    member private this.GenerateIrAssemblyCode() = {
        Functions=this.GenerateBinaryCode(_functions)
        Warnings = checkForWarnings(_functions)
    }

    member this.GenerateBinaryCode(functions: List<IrFunction>) =
        let symbolTable = new SymbolTable()

        // add all function names and labels to the symbol table, in order to be 
        // able to correctly assemble specific VM opCode
        addLabelNamesToSymbolTable(symbolTable, functions)
                
        // assemble the code
        let vmFunctions =
            orderFunctions(functions, settings)
            |> Seq.map(generateFunctionVmOpCodes(symbolTable, settings))
            |> Seq.toList

        // fix the offset
        symbolTable.FixPlaceholders(vmFunctions)

        // obfuscate
        obfuscate(vmFunctions, settings)

        vmFunctions 

    member this.Assemble(instructions: Action<Ctx> array) = 
        _functions.Clear()
        _currentIp <- 0

        let ctx = new Ctx(Settings=settings)

        // complete all instructions in the given context
        instructions
        |> Seq.iter(fun irFunction -> irFunction.Invoke(ctx))

        _functions <- 
            new List<IrFunction>(ctx.Functions
            |> Seq.map(fun kv ->
                let (funcName, funOpCodes) = (kv.Key, kv.Value |> Seq.filter(Option.isSome) |> Seq.map(Option.get))
                let irFunction = new IrFunction(funcName)
                irFunction.Body.AddRange(funOpCodes)
                irFunction
            ))

        // generate VM opcode
        this.GenerateIrAssemblyCode()

    member this.Assemble(irCode: String) =
        _functions.Clear()
        _currentIp <- 0
        
        parseCode(irCode)

        // generate VM opcode
        this.GenerateIrAssemblyCode()

