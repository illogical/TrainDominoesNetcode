using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Syntax inventory, not a C# control-flow prover. Unsupported shapes emit diagnostics.
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var files = JsonSerializer.Deserialize<List<SourceFile>>(Console.In.ReadToEnd(),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
var analyzer = new Analyzer(files);
Console.WriteLine(JsonSerializer.Serialize(analyzer.Run(), options));

record SourceFile(string Path, string Text);
record StateNode(string Name, string? Base, bool Abstract, string File, int Line);
record Transition(string Source, string Target, string Event, string Handler, string Guard,
    bool Reuse, string Owner, string File, int Line);
record Reaction(string Source, string Event, string Handler, string Owner, string File,
    int Line, string[] Actions);
record Inventory(StateNode[] States, Transition[] Transitions, Reaction[] Reactions, string[] Warnings);
record ClassInfo(ClassDeclarationSyntax Syntax, string File)
{
    public string Name => Syntax.Identifier.ValueText;
    public string? Base => Syntax.BaseList?.Types.FirstOrDefault()?.Type.ToString().Split('.').Last();
}

sealed class Analyzer
{
    readonly Dictionary<string, ClassInfo> classes = new();
    readonly List<Transition> transitions = new();
    readonly List<Reaction> reactions = new();
    readonly SortedSet<string> warnings = new(StringComparer.Ordinal);
    readonly HashSet<string> visitedSwitches = new();

    public Analyzer(IEnumerable<SourceFile> files)
    {
        foreach (var file in files.OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            var tree = CSharpSyntaxTree.ParseText(file.Text, path: file.Path);
            foreach (var error in tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error))
                warnings.Add($"Parse error: {error}");
            if (tree.GetRoot().DescendantTrivia(descendIntoTrivia: true).Any(x => x.GetStructure() is IfDirectiveTriviaSyntax))
                warnings.Add($"{file.Path}: conditional compilation exists; default preprocessor branch only, review other configurations manually.");
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var info = new ClassInfo(declaration, file.Path);
                if (!classes.TryAdd(info.Name, info)) warnings.Add($"Duplicate class name: {info.Name}; manual mapping required.");
            }
        }
    }

    public Inventory Run()
    {
        var states = classes.Values.Where(x => x.Name != "GameStateBase" && IsState(x)).OrderBy(x => x.Name).ToArray();
        foreach (var state in states.Where(x => !x.Syntax.Modifiers.Any(SyntaxKind.AbstractKeyword)))
        {
            var entry = FindMethod(state, "EnterState");
            if (entry != null)
            {
                AnalyzeMethod(state.Name, entry.Value.Owner, entry.Value.Method, "EnterState", "", new());
                var subscriptions = Subscriptions(entry.Value.Owner, entry.Value.Method, "EnterState", new());
                var leave = FindMethod(state, "LeaveState");
                var removals = leave == null ? new List<Binding>() : Subscriptions(leave.Value.Owner, leave.Value.Method, "LeaveState", new());
                foreach (var binding in subscriptions)
                {
                    if (!removals.Any(x => x.Event == binding.Event && x.Handler == binding.Handler))
                        Warn(binding.Owner, binding.Node, $"{state.Name}: no matching LeaveState unsubscribe for {binding.Event} / {binding.Handler}");
                    var handler = FindMethod(binding.Owner, binding.Handler);
                    if (handler == null) { Warn(binding.Owner, binding.Node, $"Unresolved event handler: {binding.Handler}"); continue; }
                    var (owner, method) = handler.Value;
                    reactions.Add(new(state.Name, binding.Event, binding.Handler, owner.Name, owner.File, Line(method),
                        method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Where(x => x.Expression.ToString().StartsWith("ctx.") && CallName(x) != "SwitchState")
                            .Select(x => x.NormalizeWhitespace().ToString()).Distinct().ToArray()));
                    AnalyzeMethod(state.Name, owner, method, binding.Event, "", new());
                }
            }
            var update = FindMethod(state, "UpdateState");
            if (update != null) AnalyzeMethod(state.Name, update.Value.Owner, update.Value.Method, "UpdateState (caller wiring not verified)", "", new());
        }

        // The coordinator is a lifetime router, not a state. Its routes are possible
        // from every concrete mode; guards remain source expressions, never evaluated here.
        if (classes.TryGetValue("PlayerStateCoordinator", out var coordinator))
        {
            var start = FindMethod(coordinator, "Start");
            var dispose = FindMethod(coordinator, "Dispose");
            var reconcile = FindMethod(coordinator, "Reconcile");
            if (start == null || dispose == null || reconcile == null)
                Warn(coordinator, coordinator.Syntax, "Coordinator requires Start, Dispose, and Reconcile for lifetime routing coverage.");
            var subscriptions = start == null ? new List<Binding>() : Subscriptions(coordinator, start.Value.Method, "Start", new());
            var removals = dispose == null ? new List<Binding>() : Subscriptions(coordinator, dispose.Value.Method, "Dispose", new());
            foreach (var binding in subscriptions)
                if (!removals.Any(x => x.Event == binding.Event && x.Handler == binding.Handler))
                    Warn(binding.Owner, binding.Node, $"PlayerStateCoordinator: no matching Dispose unsubscribe for {binding.Event} / {binding.Handler}");
            foreach (var state in states.Where(x => !x.Syntax.Modifiers.Any(SyntaxKind.AbstractKeyword)))
            {
                if (start != null) AnalyzeMethod(state.Name, coordinator, start.Value.Method, "Start", "", new());
                if (reconcile != null) AnalyzeMethod(state.Name, coordinator, reconcile.Value.Method,
                    "Reconcile (public caller wiring not verified)", "", new());
                foreach (var binding in subscriptions)
                {
                    var handler = FindMethod(binding.Owner, binding.Handler);
                    if (handler == null) { Warn(binding.Owner, binding.Node, $"Unresolved event handler: {binding.Handler}"); continue; }
                    var (owner, method) = handler.Value;
                    reactions.Add(new(state.Name, binding.Event, binding.Handler, owner.Name, owner.File, Line(method),
                        method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Where(x => CallName(x) != "SwitchState")
                            .Select(x => x.NormalizeWhitespace().ToString()).Distinct().ToArray()));
                    AnalyzeMethod(state.Name, owner, method, binding.Event, "", new());
                }
            }
        }

        if (classes.TryGetValue("GameStateContext", out var context))
        {
            foreach (var ctor in context.Syntax.Members.OfType<ConstructorDeclarationSyntax>())
                if (ctor.Body != null) WalkBlock("[*]", context, ".ctor", "Initialize", ctor.Body.Statements, new() { "" }, new());
        }
        else warnings.Add("No GameStateContext found: initial-state mapping is unavailable.");

        foreach (var info in classes.Values)
            foreach (var call in info!.Syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => CallName(x) == "SwitchState"))
                if (!visitedSwitches.Contains(Key(info, call))) Warn(info, call,  $"{info.Name}: SwitchState call not reached through a recognized entry/update/event/coordinator path; inspect manually.");

        return new(states.Select(x => new StateNode(x.Name, x.Base, x.Syntax.Modifiers.Any(SyntaxKind.AbstractKeyword), x.File, Line(x.Syntax))).ToArray(),
            transitions.Distinct().OrderBy(x => x.Source).ThenBy(x => x.Event).ThenBy(x => x.Target).ThenBy(x => x.Guard).ToArray(),
            reactions.Distinct().OrderBy(x => x.Source).ThenBy(x => x.Event).ToArray(), warnings.ToArray());
    }

    bool IsState(ClassInfo info, HashSet<string>? seen = null)
    {
        seen ??= new();
        if (!seen.Add(info.Name)) return false;
        return info.Base == "GameStateBase" || (info.Base != null && classes.TryGetValue(info.Base, out var parent) && IsState(parent, seen));
    }

    (ClassInfo Owner, MethodDeclarationSyntax Method)? FindMethod(ClassInfo info, string name)
    {
        var seen = new HashSet<string>();
        while (seen.Add(info.Name))
        {
            var methods = info.Syntax.Members.OfType<MethodDeclarationSyntax>().Where(x => x.Identifier.ValueText == name).ToArray();
            if (methods.Length > 1) { Warn(info, methods[0], $"Overloaded method {name}: manual resolution required."); return null; }
            if (methods.Length == 1) return (info, methods[0]);
            if (info.Base == null || !classes.TryGetValue(info.Base, out info!)) break;
        }
        return null;
    }

    record Binding(string Event, string Handler, ClassInfo Owner, AssignmentExpressionSyntax Node);
    List<Binding> Subscriptions(ClassInfo owner, MethodDeclarationSyntax method, string lifecycle, HashSet<string> seen)
    {
        var result = new List<Binding>();
        if (!seen.Add(owner.Name + "." + lifecycle)) { Warn(owner, method, "Lifecycle inheritance cycle."); return result; }
        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            bool kind = lifecycle is "EnterState" or "Start" ? assignment.IsKind(SyntaxKind.AddAssignmentExpression) : assignment.IsKind(SyntaxKind.SubtractAssignmentExpression);
            if (!kind) continue;
            if (assignment.Right is not IdentifierNameSyntax handler)
            { Warn(owner, assignment, "Inline/delegated event subscription needs manual mapping."); continue; }
            if (assignment.Ancestors().Any(x => x is IfStatementSyntax or SwitchStatementSyntax or ForEachStatementSyntax or ForStatementSyntax))
                Warn(owner, assignment, "Conditional event subscription: graph shows a possible subscription, not guaranteed availability.");
            result.Add(new(assignment.Left.ToString(), handler.Identifier.ValueText, owner, assignment));
        }
        foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression.ToString() == "base." + lifecycle && owner.Base != null && classes.TryGetValue(owner.Base, out var parent))
            {
                var inherited = FindMethod(parent, lifecycle);
                if (inherited != null) result.AddRange(Subscriptions(inherited.Value.Owner, inherited.Value.Method, lifecycle, seen));
            }
            else if (CallName(call) != "TraceExecution" && call.Expression is IdentifierNameSyntax && FindMethod(owner, CallName(call)) is { } helper &&
                helper.Method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(x => x.IsKind(SyntaxKind.AddAssignmentExpression) || x.IsKind(SyntaxKind.SubtractAssignmentExpression)))
                Warn(owner, call, "Subscription helper is outside direct lifecycle mapping; inspect manually.");
        }
        return result;
    }

    void AnalyzeMethod(string source, ClassInfo owner, MethodDeclarationSyntax method, string trigger, string guard, HashSet<string> stack)
    {
        string key = owner.Name + "." + method.Identifier.ValueText;
        if (!stack.Add(key)) { Warn(owner, method, "Recursive helper path: " + key); return; }
        if (method.Modifiers.Any(SyntaxKind.AsyncKeyword)) Warn(owner, method, "Async path: timing and continuations require manual review.");
        if (method.Body != null) WalkBlock(source, owner, method.Identifier.ValueText, trigger, method.Body.Statements, new() { guard }, stack);
        else if (method.ExpressionBody != null) WalkExpression(source, owner, method.Identifier.ValueText, trigger, guard, method.ExpressionBody.Expression, stack);
        stack.Remove(key);
    }

    List<string> WalkBlock(string source, ClassInfo owner, string handler, string trigger,
        SyntaxList<StatementSyntax> statements, List<string> paths, HashSet<string> stack)
    {
        foreach (var statement in statements)
            paths = paths.SelectMany(path => WalkStatement(source, owner, handler, trigger, path, statement, stack)).Distinct().ToList();
        return paths;
    }

    List<string> WalkStatement(string source, ClassInfo owner, string handler, string trigger, string guard, StatementSyntax statement, HashSet<string> stack)
    {
        if (statement is BlockSyntax block) return WalkBlock(source, owner, handler, trigger, block.Statements, new() { guard }, stack);
        if (statement is IfStatementSyntax condition)
        {
            string text = condition.Condition.NormalizeWhitespace().ToString();
            var yes = WalkStatement(source, owner, handler, trigger, And(guard, text), condition.Statement, stack);
            var no = condition.Else == null ? new List<string> { And(guard, "!(" + text + ")") }
                : WalkStatement(source, owner, handler, trigger, And(guard, "!(" + text + ")"), condition.Else.Statement, stack);
            // Both branches continue: subsequent calls need no additional guard for this if.
            if (yes.Count == 1 && no.Count == 1 && yes[0] == And(guard, text) && no[0] == And(guard, "!(" + text + ")")) return new() { guard };
            return yes.Concat(no).ToList();
        }
        if (statement is ReturnStatementSyntax or ThrowStatementSyntax) return new();
        if (statement is not (ExpressionStatementSyntax or LocalDeclarationStatementSyntax or EmptyStatementSyntax))
        {
            if (statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any())
                Warn(owner, statement, $"Unsupported control flow {statement.Kind()}: guard/order requires manual review.");
            WalkExpression(source, owner, handler, trigger, And(guard, "MANUAL: " + statement.Kind()), statement, stack);
            return new() { guard };
        }
        WalkExpression(source, owner, handler, trigger, guard, statement, stack);
        return new() { guard };
    }

    void WalkExpression(string source, ClassInfo owner, string handler, string trigger, string guard, SyntaxNode node, HashSet<string> stack)
    {
        foreach (var call in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (call.Ancestors().TakeWhile(x => x != node).Any(x => x is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            { if (CallName(call) == "SwitchState") Warn(owner, call, "Deferred lambda/local function transition: timing needs manual mapping."); continue; }
            if (CallName(call) == "SwitchState")
            {
                visitedSwitches.Add(Key(owner, call));
                if (call.Expression is MemberAccessExpressionSyntax receiver && receiver.Expression.ToString() != "ctx")
                    Warn(owner, call, "Transition receiver differs from ctx; verify the owning context manually.");
                if (call.Ancestors().TakeWhile(x => x is not StatementSyntax).Any(x => x is ConditionalExpressionSyntax))
                    Warn(owner, call, "Conditional expression transition: guard needs manual review.");
                var first = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                var creation = first is LambdaExpressionSyntax lambda ? lambda.Body as ObjectCreationExpressionSyntax : first as ObjectCreationExpressionSyntax;
                if (creation == null) { Warn(owner, call, "Unresolved transition factory; expected a direct lambda/new expression."); continue; }
                string target = creation.Type.ToString().Split('.').Last();
                if (!classes.TryGetValue(target, out var destination) || !IsState(destination) || destination.Syntax.Modifiers.Any(SyntaxKind.AbstractKeyword))
                    Warn(owner, call, "Transition target is not a known concrete player state: " + target);
                var reuse = call.ArgumentList.Arguments.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "reuse");
                if (reuse != null && reuse.Expression is not LiteralExpressionSyntax) Warn(owner, call, "Dynamic reuse policy requires manual review.");
                transitions.Add(new(source, target, trigger, handler, guard, reuse?.Expression.IsKind(SyntaxKind.FalseLiteralExpression) != true,
                    owner.Name, owner.File, Line(call)));
            }
            else if (call.Expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText != "TraceExecution")
            {
                var helper = FindMethod(owner, identifier.Identifier.ValueText);
                if (helper != null) AnalyzeMethod(source, helper.Value.Owner, helper.Value.Method, trigger, guard, stack);
            }
            else if (call.Expression is MemberAccessExpressionSyntax inherited && inherited.Expression is BaseExpressionSyntax &&
                     owner.Base != null && classes.TryGetValue(owner.Base, out var parent))
            {
                var helper = FindMethod(parent, CallName(call));
                if (helper != null) AnalyzeMethod(source, helper.Value.Owner, helper.Value.Method, trigger, guard, stack);
            }
        }
    }

    static string And(string first, string next) => first.Length == 0 ? next : $"({first}) && ({next})";
    static string CallName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => ""
    };
    static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    static string Key(ClassInfo owner, SyntaxNode node) => owner.File + ":" + node.SpanStart;
    void Warn(ClassInfo owner, SyntaxNode node, string message) => warnings.Add($"{owner.File}:{Line(node)}: {message}");
}
