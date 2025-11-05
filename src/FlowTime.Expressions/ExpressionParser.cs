using System.Collections.Generic;
using System.Globalization;

namespace FlowTime.Expressions;

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
/// Factor      = Number | Array | NodeRef | FunctionCall | '(' Expression ')'
/// FunctionCall = Identifier '(' (Expression (',' Expression)*)? ')'
/// NodeRef     = Identifier
/// Array       = '[' (Number (',' Number)*)? ']'
/// </summary>
public class ExpressionParser
{
    private readonly string expression;
    private int position;

    public ExpressionParser(string expression)
    {
        this.expression = expression ?? throw new ArgumentNullException(nameof(expression));
        position = 0;
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
            
            if (position < expression.Length)
            {
                throw new ExpressionParseException($"Unexpected character '{CurrentChar}'", position, expression);
            }
            
            return result;
        }
        catch (ExpressionParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExpressionParseException($"Unexpected error: {ex.Message}", position, expression);
        }
    }
    
    private ExpressionNode ParseExpression()
    {
        var left = ParseTerm();
        
        while (CurrentChar is '+' or '-')
        {
            var op = CurrentChar == '+' ? BinaryOperator.Add : BinaryOperator.Subtract;
            var opPos = position;
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
            var opPos = position;
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
                throw new ExpressionParseException("Expected ')'", position, expression);
            }
            
            Advance(); // consume ')'
            SkipWhitespace();
            return expr;
        }

        // Array literal
        if (CurrentChar == '[')
        {
            return ParseArrayLiteral();
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
        
        throw new ExpressionParseException($"Unexpected character '{CurrentChar}'", position, expression);
    }

    private ExpressionNode ParseArrayLiteral()
    {
        var startPos = position;
        Advance(); // consume '['
        SkipWhitespace();

        var values = new List<double>();

        if (CurrentChar == ']')
        {
            Advance();
            SkipWhitespace();
            return new ArrayLiteralNode
            {
                Values = values,
                Position = startPos
            };
        }

        while (true)
        {
            var element = ParseNumber();
            if (element is not LiteralNode literal)
            {
                throw new ExpressionParseException("Array elements must be numeric literals", element.Position, expression);
            }

            values.Add(literal.Value);

            if (CurrentChar == ',')
            {
                Advance();
                SkipWhitespace();
                continue;
            }

            if (CurrentChar == ']')
            {
                Advance();
                SkipWhitespace();
                break;
            }

            throw new ExpressionParseException("Expected ',' or ']'", position, expression);
        }

        return new ArrayLiteralNode
        {
            Values = values,
            Position = startPos
        };
    }
    
    private ExpressionNode ParseNumber()
    {
        var start = position;
        
        // Integer part
        while (position < expression.Length && char.IsDigit(expression[position]))
        {
            position++;
        }
        
        // Decimal part
        if (position < expression.Length && expression[position] == '.')
        {
            position++;
            if (position >= expression.Length || !char.IsDigit(expression[position]))
            {
                throw new ExpressionParseException("Expected digit after decimal point", position, expression);
            }
            
            while (position < expression.Length && char.IsDigit(expression[position]))
            {
                position++;
            }
        }
        
        var numberText = expression.Substring(start, position - start);
        
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new ExpressionParseException($"Invalid number format: {numberText}", start, expression);
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
        var start = position;
        
        // First character must be letter or underscore
        if (!char.IsLetter(CurrentChar) && CurrentChar != '_')
        {
            throw new ExpressionParseException("Expected identifier", position, expression);
        }
        
        position++; // Don't call Advance() here to avoid skipping whitespace
        
        // Subsequent characters can be letters, digits, or underscores
        while (position < expression.Length && 
               (char.IsLetterOrDigit(expression[position]) || expression[position] == '_'))
        {
            position++;
        }
        
        var identifier = expression.Substring(start, position - start);
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
                    throw new ExpressionParseException("Unexpected ',' before ')'", position - 1, expression);
                }
            }
            else if (CurrentChar == ')')
            {
                break;
            }
            else
            {
                throw new ExpressionParseException("Expected ',' or ')'", position, expression);
            }
        } while (true);
        
        if (CurrentChar != ')')
        {
            throw new ExpressionParseException("Expected ')'", position, expression);
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
    
    private char CurrentChar => position < expression.Length ? expression[position] : '\0';
    
    private void Advance()
    {
        if (position < expression.Length)
        {
            position++;
        }
    }
    
    private void SkipWhitespace()
    {
        while (position < expression.Length && char.IsWhiteSpace(expression[position]))
        {
            position++;
        }
    }
}
