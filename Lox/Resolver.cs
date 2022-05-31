namespace Lox;

public sealed class Resolver : ExpressionVisitor<object>, StatementVisitor<object>
{
	public Resolver(Interpreter interpreter) => this.interpreter = interpreter;
	private readonly Interpreter interpreter;
	private readonly Stack<Dictionary<string, bool>> scopes = new();
	private FunctionType currentFunction = FunctionType.None;

	public object VisitBlockStatement(Statement.BlockStatement blockStatement)
	{
		BeginScope();
		Resolve(blockStatement.statements);
		EndScope();
		return new object();
	}

	public object VisitExpressionStatement(Statement.ExpressionStatement expressionStatement)
	{
		Resolve(expressionStatement.expression);
		return new object();
	}

	public object VisitFunctionStatement(Statement.FunctionStatement functionStatement)
	{
		Declare(functionStatement.name);
		Define(functionStatement.name);
		ResolveFunction(functionStatement, FunctionType.Function);
		return new object();
	}

	public object VisitIfStatement(Statement.IfStatement ifStatement)
	{
		Resolve(ifStatement.condition);
		Resolve(ifStatement.thenStatement);
		if (ifStatement.elseStatement != null)
			Resolve(ifStatement.elseStatement);
		return new object();
	}

	public object VisitPrintStatement(Statement.PrintStatement printStatement)
	{
		Resolve(printStatement.expression);
		return new object();
	}

	public object VisitReturnStatement(Statement.ReturnStatement returnStatement)
	{
		if (currentFunction == FunctionType.None)
			throw new InvalidOperationException("Cannot return from top level code " +
				returnStatement.keyword);
		if (returnStatement.value != null)
			Resolve(returnStatement.value);
		return new object();
	}

	private void ResolveFunction(Statement.FunctionStatement functionStatement, FunctionType type)
	{
		var enclosingFunction = currentFunction;
		currentFunction = type;
		BeginScope();
		foreach (var param in functionStatement.functionParams)
		{
			Declare(param);
			Define(param);
		}
		Resolve(functionStatement.body);
		EndScope();
		currentFunction = enclosingFunction;
	}

	private void BeginScope() => scopes.Push(new Dictionary<string, bool>());

	public void Resolve(List<Statement> statements)
	{
		foreach (var statement in statements)
			Resolve(statement);
	}

	private void Resolve(Statement statement) => statement.Accept(this);
	private void EndScope() => scopes.Pop();

	public object VisitVariableStatement(Statement.VariableStatement variableStatement)
	{
		Declare(variableStatement.name);
		if (variableStatement.initializer != null)
			Resolve(variableStatement.initializer);
		Define(variableStatement.name);
		return new object();
	}

	public object VisitAssignmentExpression(Expression.AssignmentExpression assignmentExpression)
	{
		Resolve(assignmentExpression.value);
		ResolveLocal(assignmentExpression, assignmentExpression.name);
		return new object();
	}

	public object VisitBinaryExpression(Expression.BinaryExpression binaryExpression)
	{
		Resolve(binaryExpression.LeftExpression);
		Resolve(binaryExpression.RightExpression);
		return new object();
	}

	public object VisitCallExpression(Expression.CallExpression callExpression)
	{
		Resolve(callExpression.callee);
		foreach (var argument in callExpression.arguments)
			Resolve(argument);
		return new object();
	}

	public object VisitGroupingExpression(Expression.GroupingExpression groupingExpression)
	{
		Resolve(groupingExpression.expression);
		return new object();
	}

	public object VisitLiteralExpression(Expression.LiteralExpression literal) => new();

	public object VisitLogicalExpression(Expression.LogicalExpression logicalExpression)
	{
		Resolve(logicalExpression.left);
		Resolve(logicalExpression.right);
		return new object();
	}

	public object VisitUnaryExpression(Expression.UnaryExpression unaryExpression)
	{
		Resolve(unaryExpression.RightExpression);
		return new object();
	}

	private void Declare(Token name)
	{
		if (scopes.Count == 0)
			return;
		var scope = scopes.Peek();
		if (scope.ContainsKey(name.Lexeme))
			throw new Environment.DuplicateVariableName(name.Lexeme);
		scope.Add(name.Lexeme, false);
	}

	public object VisitWhileStatement(Statement.WhileStatement whileStatement)
	{
		Resolve(whileStatement.condition);
		Resolve(whileStatement.bodyStatement);
		return new object();
	}

	private void Resolve(Expression expression) => expression.Accept(this);

	private void Define(Token name)
	{
		if (scopes.Count == 0)
			return;
		scopes.Peek()[name.Lexeme] = true;
	}

	public object VisitVariableExpression(Expression.VariableExpression variableExpression)
	{
		if (scopes.Count > 0 && !scopes.Peek()[variableExpression.name.Lexeme])
			throw new CannotReadLocalVariableInInitializer(variableExpression.name);
		ResolveLocal(variableExpression, variableExpression.name);
		return new object();
	}

	private sealed class CannotReadLocalVariableInInitializer : Exception
	{
		public CannotReadLocalVariableInInitializer(Token variableExpressionName) : base(variableExpressionName.Lexeme) { }
	}

	private void ResolveLocal(Expression expression, Token name)
	{
		for (var i = scopes.Count - 1; i >= 0; i--)
			if (scopes.ElementAt(i).ContainsKey(name.Lexeme))
			{
				interpreter.Resolve(expression, scopes.Count - 1 - i);
				return;
			}
	}
}

internal enum FunctionType
{
	None,
	Function
}