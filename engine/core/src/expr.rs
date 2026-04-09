//! Expression parser for FlowTime expressions.
//!
//! Recursive descent parser producing an AST. Ports the C# `ExpressionParser`
//! from `src/FlowTime.Expressions/ExpressionParser.cs`.
//!
//! Grammar:
//!   Expression  = Term (('+' | '-') Term)*
//!   Term        = Factor (('*' | '/') Factor)*
//!   Factor      = Number | Array | NodeRef | FunctionCall | '(' Expression ')'
//!   FunctionCall = Identifier '(' (Expression (',' Expression)*)? ')'
//!   NodeRef     = Identifier
//!   Array       = '[' (Number (',' Number)*)? ']'

/// AST node for FlowTime expressions.
#[derive(Debug, Clone, PartialEq)]
pub enum Expr {
    Literal(f64),
    ArrayLiteral(Vec<f64>),
    NodeRef(String),
    BinaryOp {
        op: BinaryOp,
        left: Box<Expr>,
        right: Box<Expr>,
    },
    FunctionCall {
        name: String,
        args: Vec<Expr>,
    },
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BinaryOp {
    Add,
    Subtract,
    Multiply,
    Divide,
}

/// Parse error with position information.
#[derive(Debug, Clone)]
pub struct ParseError {
    pub message: String,
    pub position: usize,
}

impl std::fmt::Display for ParseError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "Parse error at position {}: {}", self.position, self.message)
    }
}

impl std::error::Error for ParseError {}

/// Parse an expression string into an AST.
pub fn parse(input: &str) -> Result<Expr, ParseError> {
    let mut parser = Parser::new(input);
    let expr = parser.parse_expression()?;
    parser.skip_whitespace();
    if parser.pos < parser.input.len() {
        return Err(parser.error(&format!(
            "Unexpected character '{}'",
            parser.input[parser.pos..].chars().next().unwrap()
        )));
    }
    Ok(expr)
}

struct Parser<'a> {
    input: &'a str,
    pos: usize,
}

impl<'a> Parser<'a> {
    fn new(input: &'a str) -> Self {
        Self { input, pos: 0 }
    }

    fn error(&self, message: &str) -> ParseError {
        ParseError {
            message: message.to_string(),
            position: self.pos,
        }
    }

    fn skip_whitespace(&mut self) {
        while self.pos < self.input.len() && self.input.as_bytes()[self.pos].is_ascii_whitespace() {
            self.pos += 1;
        }
    }

    fn peek(&mut self) -> Option<char> {
        self.skip_whitespace();
        self.input[self.pos..].chars().next()
    }

    fn consume(&mut self, expected: char) -> Result<(), ParseError> {
        self.skip_whitespace();
        if self.pos < self.input.len() && self.input.as_bytes()[self.pos] == expected as u8 {
            self.pos += 1;
            Ok(())
        } else {
            Err(self.error(&format!("Expected '{expected}'")))
        }
    }

    // Expression = Term (('+' | '-') Term)*
    fn parse_expression(&mut self) -> Result<Expr, ParseError> {
        let mut left = self.parse_term()?;
        loop {
            self.skip_whitespace();
            match self.peek() {
                Some('+') => {
                    self.pos += 1;
                    let right = self.parse_term()?;
                    left = Expr::BinaryOp {
                        op: BinaryOp::Add,
                        left: Box::new(left),
                        right: Box::new(right),
                    };
                }
                Some('-') => {
                    // Distinguish unary minus (part of a number) from binary subtraction.
                    // Binary subtraction only if left is already parsed (always true here).
                    self.pos += 1;
                    let right = self.parse_term()?;
                    left = Expr::BinaryOp {
                        op: BinaryOp::Subtract,
                        left: Box::new(left),
                        right: Box::new(right),
                    };
                }
                _ => break,
            }
        }
        Ok(left)
    }

    // Term = Factor (('*' | '/') Factor)*
    fn parse_term(&mut self) -> Result<Expr, ParseError> {
        let mut left = self.parse_factor()?;
        loop {
            match self.peek() {
                Some('*') => {
                    self.pos += 1;
                    let right = self.parse_factor()?;
                    left = Expr::BinaryOp {
                        op: BinaryOp::Multiply,
                        left: Box::new(left),
                        right: Box::new(right),
                    };
                }
                Some('/') => {
                    self.pos += 1;
                    let right = self.parse_factor()?;
                    left = Expr::BinaryOp {
                        op: BinaryOp::Divide,
                        left: Box::new(left),
                        right: Box::new(right),
                    };
                }
                _ => break,
            }
        }
        Ok(left)
    }

    // Factor = Number | Array | NodeRef | FunctionCall | '(' Expression ')' | UnaryMinus
    fn parse_factor(&mut self) -> Result<Expr, ParseError> {
        self.skip_whitespace();
        if self.pos >= self.input.len() {
            return Err(self.error("Unexpected end of expression"));
        }

        let ch = self.input.as_bytes()[self.pos];

        // Parenthesized expression
        if ch == b'(' {
            self.pos += 1;
            let expr = self.parse_expression()?;
            self.consume(')')?;
            return Ok(expr);
        }

        // Array literal: [1.0, 2.0, ...]
        if ch == b'[' {
            return self.parse_array_literal();
        }

        // Unary minus
        if ch == b'-' {
            self.pos += 1;
            let factor = self.parse_factor()?;
            return Ok(Expr::BinaryOp {
                op: BinaryOp::Subtract,
                left: Box::new(Expr::Literal(0.0)),
                right: Box::new(factor),
            });
        }

        // Number (starts with digit or '.')
        if ch.is_ascii_digit() || ch == b'.' {
            return self.parse_number();
        }

        // Identifier: could be NodeRef or FunctionCall
        if ch.is_ascii_alphabetic() || ch == b'_' {
            return self.parse_identifier_or_function();
        }

        Err(self.error(&format!("Unexpected character '{}'", ch as char)))
    }

    fn parse_number(&mut self) -> Result<Expr, ParseError> {
        let start = self.pos;
        while self.pos < self.input.len() {
            let ch = self.input.as_bytes()[self.pos];
            if ch.is_ascii_digit() || ch == b'.' || ch == b'e' || ch == b'E' || ch == b'-' || ch == b'+' {
                // Handle e/E only if preceded by a digit (scientific notation)
                if (ch == b'-' || ch == b'+') && self.pos > start {
                    let prev = self.input.as_bytes()[self.pos - 1];
                    if prev != b'e' && prev != b'E' {
                        break;
                    }
                }
                self.pos += 1;
            } else {
                break;
            }
        }
        let text = &self.input[start..self.pos];
        text.parse::<f64>()
            .map(Expr::Literal)
            .map_err(|_| ParseError {
                message: format!("Invalid number: '{text}'"),
                position: start,
            })
    }

    fn parse_array_literal(&mut self) -> Result<Expr, ParseError> {
        self.consume('[')?;
        let mut values = Vec::new();
        self.skip_whitespace();

        if self.peek() == Some(']') {
            self.pos += 1;
            return Ok(Expr::ArrayLiteral(values));
        }

        loop {
            self.skip_whitespace();
            let start = self.pos;

            // Handle negative numbers in arrays
            let negative = if self.peek() == Some('-') {
                self.pos += 1;
                true
            } else {
                false
            };

            self.skip_whitespace();
            let num_start = self.pos;
            while self.pos < self.input.len() {
                let ch = self.input.as_bytes()[self.pos];
                if ch.is_ascii_digit() || ch == b'.' || ch == b'e' || ch == b'E' {
                    self.pos += 1;
                } else {
                    break;
                }
            }
            let text = &self.input[num_start..self.pos];
            let val: f64 = text.parse().map_err(|_| ParseError {
                message: format!("Invalid number in array: '{text}'"),
                position: start,
            })?;
            values.push(if negative { -val } else { val });

            self.skip_whitespace();
            match self.peek() {
                Some(',') => {
                    self.pos += 1;
                }
                Some(']') => {
                    self.pos += 1;
                    break;
                }
                _ => return Err(self.error("Expected ',' or ']' in array literal")),
            }
        }
        Ok(Expr::ArrayLiteral(values))
    }

    fn parse_identifier_or_function(&mut self) -> Result<Expr, ParseError> {
        let start = self.pos;
        while self.pos < self.input.len() {
            let ch = self.input.as_bytes()[self.pos];
            if ch.is_ascii_alphanumeric() || ch == b'_' {
                self.pos += 1;
            } else {
                break;
            }
        }
        let name = self.input[start..self.pos].to_string();

        // Check if this is a function call
        self.skip_whitespace();
        if self.pos < self.input.len() && self.input.as_bytes()[self.pos] == b'(' {
            self.pos += 1; // consume '('
            let mut args = Vec::new();
            self.skip_whitespace();

            if self.peek() != Some(')') {
                loop {
                    args.push(self.parse_expression()?);
                    self.skip_whitespace();
                    match self.peek() {
                        Some(',') => {
                            self.pos += 1;
                        }
                        Some(')') => break,
                        _ => return Err(self.error("Expected ',' or ')' in function arguments")),
                    }
                }
            }
            self.consume(')')?;
            Ok(Expr::FunctionCall { name, args })
        } else {
            Ok(Expr::NodeRef(name))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_literal() {
        assert_eq!(parse("100.0").unwrap(), Expr::Literal(100.0));
        assert_eq!(parse("42").unwrap(), Expr::Literal(42.0));
    }

    #[test]
    fn parse_node_ref() {
        assert_eq!(parse("capacity").unwrap(), Expr::NodeRef("capacity".into()));
    }

    #[test]
    fn parse_binary_add() {
        assert_eq!(
            parse("a + b").unwrap(),
            Expr::BinaryOp {
                op: BinaryOp::Add,
                left: Box::new(Expr::NodeRef("a".into())),
                right: Box::new(Expr::NodeRef("b".into())),
            }
        );
    }

    #[test]
    fn parse_precedence_mul_add() {
        // a * b + c → Add(Mul(a, b), c)
        let expr = parse("a * b + c").unwrap();
        assert_eq!(
            expr,
            Expr::BinaryOp {
                op: BinaryOp::Add,
                left: Box::new(Expr::BinaryOp {
                    op: BinaryOp::Multiply,
                    left: Box::new(Expr::NodeRef("a".into())),
                    right: Box::new(Expr::NodeRef("b".into())),
                }),
                right: Box::new(Expr::NodeRef("c".into())),
            }
        );
    }

    #[test]
    fn parse_parenthesized() {
        // (a + b) * c → Mul(Add(a, b), c)
        let expr = parse("(a + b) * c").unwrap();
        assert_eq!(
            expr,
            Expr::BinaryOp {
                op: BinaryOp::Multiply,
                left: Box::new(Expr::BinaryOp {
                    op: BinaryOp::Add,
                    left: Box::new(Expr::NodeRef("a".into())),
                    right: Box::new(Expr::NodeRef("b".into())),
                }),
                right: Box::new(Expr::NodeRef("c".into())),
            }
        );
    }

    #[test]
    fn parse_shift_function() {
        let expr = parse("SHIFT(demand, 1)").unwrap();
        assert_eq!(
            expr,
            Expr::FunctionCall {
                name: "SHIFT".into(),
                args: vec![Expr::NodeRef("demand".into()), Expr::Literal(1.0)],
            }
        );
    }

    #[test]
    fn parse_conv_with_array() {
        let expr = parse("CONV(errors, [0.0, 0.6, 0.3, 0.1])").unwrap();
        assert_eq!(
            expr,
            Expr::FunctionCall {
                name: "CONV".into(),
                args: vec![
                    Expr::NodeRef("errors".into()),
                    Expr::ArrayLiteral(vec![0.0, 0.6, 0.3, 0.1]),
                ],
            }
        );
    }

    #[test]
    fn parse_clamp_nested() {
        let expr = parse("CLAMP(queue_depth / 50, 0, 1)").unwrap();
        match expr {
            Expr::FunctionCall { name, args } => {
                assert_eq!(name, "CLAMP");
                assert_eq!(args.len(), 3);
                // First arg is queue_depth / 50
                assert!(matches!(&args[0], Expr::BinaryOp { op: BinaryOp::Divide, .. }));
                assert_eq!(args[1], Expr::Literal(0.0));
                assert_eq!(args[2], Expr::Literal(1.0));
            }
            _ => panic!("Expected FunctionCall"),
        }
    }

    #[test]
    fn parse_backpressure_expression() {
        let expr = parse("raw_arrivals * (1 - SHIFT(pressure, 1))").unwrap();
        // raw_arrivals * (1 - SHIFT(pressure, 1))
        match expr {
            Expr::BinaryOp { op: BinaryOp::Multiply, left, right } => {
                assert_eq!(*left, Expr::NodeRef("raw_arrivals".into()));
                match *right {
                    Expr::BinaryOp { op: BinaryOp::Subtract, left: l, right: r } => {
                        assert_eq!(*l, Expr::Literal(1.0));
                        assert!(matches!(*r, Expr::FunctionCall { .. }));
                    }
                    _ => panic!("Expected Subtract"),
                }
            }
            _ => panic!("Expected Multiply"),
        }
    }

    #[test]
    fn parse_min_function() {
        let expr = parse("MIN(capacity, arrivals)").unwrap();
        assert_eq!(
            expr,
            Expr::FunctionCall {
                name: "MIN".into(),
                args: vec![
                    Expr::NodeRef("capacity".into()),
                    Expr::NodeRef("arrivals".into()),
                ],
            }
        );
    }

    #[test]
    fn parse_max_with_nested_shift() {
        let expr = parse("MAX(0, SHIFT(queue_depth, 1) + arrivals)").unwrap();
        match expr {
            Expr::FunctionCall { name, args } => {
                assert_eq!(name, "MAX");
                assert_eq!(args.len(), 2);
                assert_eq!(args[0], Expr::Literal(0.0));
                // Second arg: SHIFT(...) + arrivals
                assert!(matches!(&args[1], Expr::BinaryOp { op: BinaryOp::Add, .. }));
            }
            _ => panic!("Expected FunctionCall"),
        }
    }

    #[test]
    fn parse_error_on_invalid() {
        assert!(parse("").is_err());
        assert!(parse("@invalid").is_err());
        assert!(parse("a +").is_err());
    }
}
