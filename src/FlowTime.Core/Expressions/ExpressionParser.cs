using System.Globalization;

namespace FlowTime.Core.Expressions;

/// <summary>
/// Exception thrown when expression parsing fails.
/// </summary>
public class ExpressionParseException : Exception
{
    public int Position { get; }
    public string Expression { get; }
    
    public ExpressionParseException(string message, int position, string expression) 
        : base($"Parse error at position {position}: {message}")
    {
        Position = position;
        Expression = expression;
    }
}

/// <summary>
/// Recursive descent parser for FlowTime expressions.
/// Implements the grammar:
/// Expression  = Term (('+' | '-') Term)*
/// Term        = Factor (('*' | '/') Factor)*  
/// Factor      = Number | NodeRef | FunctionCall | '(' Expression ')'
/// FunctionCall = Identifier '(' (Expression (',' Expression)*)? ')'
/// NodeRef     = Identifier
/// </summary>
public class ExpressionParser
{
    private readonly string _expression;
    private int _position;
    
    public ExpressionParser(string expression)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _position = 0;
    }
    
    /// <summary>
    /// Parse the expression into an AST.
    /// </summary>
    public ExpressionNode Parse()
    {
        try
        {
            var result = ParseExpression();
            SkipWhitespace();
            
            if (_position < _expression.Length)
            {
                throw new ExpressionParseException($"Unexpected character '{CurrentChar}'", _position, _expression);
            }
            
            return result;
        }
        catch (ExpressionParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExpressionParseException($"Unexpected error: {ex.Message}", _position, _expression);
        }
    }
    
    private ExpressionNode ParseExpression()
    {
        var left = ParseTerm();
        
        while (CurrentChar is '+' or '-')
        {
            var op = CurrentChar == '+' ? BinaryOperator.Add : BinaryOperator.Subtract;
            var opPos = _position;
            Advance();
            SkipWhitespace();
            var right = ParseTerm();
            
            left = new BinaryOpNode
            {
                Operator = op,
                Left = left,
                Right = right,
                Position = opPos
            };
        }
        
        return left;
    }
    
    private ExpressionNode ParseTerm()
    {
        var left = ParseFactor();
        
        while (CurrentChar is '*' or '/')
        {
            var op = CurrentChar == '*' ? BinaryOperator.Multiply : BinaryOperator.Divide;
            var opPos = _position;
            Advance();
            SkipWhitespace();
            var right = ParseFactor();
            
            left = new BinaryOpNode
            {
                Operator = op,
                Left = left,
                Right = right,
                Position = opPos
            };
        }
        
        return left;
    }
    
    private ExpressionNode ParseFactor()
    {
        SkipWhitespace();
        
        // Parenthesized expression
        if (CurrentChar == '(')
        {
            Advance(); // consume '('
            SkipWhitespace();
            var expr = ParseExpression();
            SkipWhitespace();
            
            if (CurrentChar != ')')
            {
                throw new ExpressionParseException("Expected ')'", _position, _expression);
            }
            
            Advance(); // consume ')'
            SkipWhitespace();
            return expr;
        }
        
        // Number
        if (char.IsDigit(CurrentChar))
        {
            return ParseNumber();
        }
        
        // Identifier (could be node reference or function call)
        if (char.IsLetter(CurrentChar) || CurrentChar == '_')
        {
            return ParseIdentifier();
        }
        
        throw new ExpressionParseException($"Unexpected character '{CurrentChar}'", _position, _expression);
    }
    
    private ExpressionNode ParseNumber()
    {
        var start = _position;
        
        // Integer part
        while (_position < _expression.Length && char.IsDigit(_expression[_position]))
        {
            _position++;
        }
        
        // Decimal part
        if (_position < _expression.Length && _expression[_position] == '.')
        {
            _position++;
            if (_position >= _expression.Length || !char.IsDigit(_expression[_position]))
            {
                throw new ExpressionParseException("Expected digit after decimal point", _position, _expression);
            }
            
            while (_position < _expression.Length && char.IsDigit(_expression[_position]))
            {
                _position++;
            }
        }
        
        var numberText = _expression.Substring(start, _position - start);
        
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new ExpressionParseException($"Invalid number format: {numberText}", start, _expression);
        }
        
        SkipWhitespace();
        
        return new LiteralNode
        {
            Value = value,
            Position = start
        };
    }
    
    private ExpressionNode ParseIdentifier()
    {
        var start = _position;
        
        // First character must be letter or underscore
        if (!char.IsLetter(CurrentChar) && CurrentChar != '_')
        {
            throw new ExpressionParseException("Expected identifier", _position, _expression);
        }
        
        _position++; // Don't call Advance() here to avoid skipping whitespace
        
        // Subsequent characters can be letters, digits, or underscores
        while (_position < _expression.Length && 
               (char.IsLetterOrDigit(_expression[_position]) || _expression[_position] == '_'))
        {
            _position++;
        }
        
        var identifier = _expression.Substring(start, _position - start);
        SkipWhitespace();
        
        // Check if this is a function call
        if (CurrentChar == '(')
        {
            return ParseFunctionCall(identifier, start);
        }
        
        // Otherwise, it's a node reference
        return new NodeReferenceNode
        {
            NodeId = identifier,
            Position = start
        };
    }
    
    private ExpressionNode ParseFunctionCall(string functionName, int startPos)
    {
        Advance(); // consume '('
        SkipWhitespace();
        
        var arguments = new List<ExpressionNode>();
        
        // Empty argument list
        if (CurrentChar == ')')
        {
            Advance();
            SkipWhitespace();
            return new FunctionCallNode
            {
                FunctionName = functionName,
                Arguments = arguments,
                Position = startPos
            };
        }
        
        // Parse arguments
        do
        {
            arguments.Add(ParseExpression());
            SkipWhitespace();
            
            if (CurrentChar == ',')
            {
                Advance();
                SkipWhitespace();
                
                // Check for trailing comma
                if (CurrentChar == ')')
                {
                    throw new ExpressionParseException("Unexpected ',' before ')'", _position - 1, _expression);
                }
            }
            else if (CurrentChar == ')')
            {
                break;
            }
            else
            {
                throw new ExpressionParseException("Expected ',' or ')'", _position, _expression);
            }
        } while (true);
        
        if (CurrentChar != ')')
        {
            throw new ExpressionParseException("Expected ')'", _position, _expression);
        }
        
        Advance(); // consume ')'
        SkipWhitespace();
        
        return new FunctionCallNode
        {
            FunctionName = functionName,
            Arguments = arguments,
            Position = startPos
        };
    }
    
    private char CurrentChar => _position < _expression.Length ? _expression[_position] : '\0';
    
    private void Advance()
    {
        if (_position < _expression.Length)
        {
            _position++;
        }
    }
    
    private void SkipWhitespace()
    {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }
}
