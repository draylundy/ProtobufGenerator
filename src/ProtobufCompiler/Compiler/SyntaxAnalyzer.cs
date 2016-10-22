﻿using ProtobufCompiler.Compiler.Errors;
using ProtobufCompiler.Compiler.Nodes;
using ProtobufCompiler.Extensions;
using ProtobufCompiler.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtobufCompiler.Compiler
{
    internal class SyntaxAnalyzer : ISyntaxAnalyzer
    {
        private readonly Queue<Token> _tokens;
        private readonly Parser _parser;
        private readonly IList<ParseError> _errors;

        internal SyntaxAnalyzer(Queue<Token> tokens)
        {
            _tokens = tokens;
            _parser = new Parser();
            _errors = new List<ParseError>();
        }

        public RootNode Analyze()
        {
            var root = new RootNode();
            while (_tokens.Any())
            {
                var topLevelStatement = ParseTopLevelStatement(root);
                if (topLevelStatement != null) root.AddChild(topLevelStatement);
            }
            root.AddErrors(_errors);
            return root;
        }

        private void BurnLine()
        {
            Token token;
            do
            {
                if (!_tokens.Any()) return;
                token = _tokens.Dequeue();
            } while (!token.Type.Equals(TokenType.EndLine));
        }

        internal Node ParseTopLevelStatement(Node root)
        {
            var token = _tokens.Peek();
            if (!(token.Type.Equals(TokenType.Comment) || token.Type.Equals(TokenType.Id)))
            {
                _errors.Add(new ParseError("Found an invalid top level statement at token ", token));
                BurnLine();
                token = _tokens.Peek();
            }

            var lexeme = token.Lexeme;

            if (_parser.IsInlineComment(lexeme)) return ParseInlineComment();

            if (_parser.IsMultiLineCommentOpen(lexeme)) return ParseMultiLineComment();

            if (_parser.IsSyntax(lexeme)) return ParseSyntax();

            if (_parser.IsImport(lexeme)) return ParseImport();

            if (_parser.IsPackage(lexeme)) return ParsePackage();

            if (_parser.IsOption(lexeme)) return ParseOption();

            if (_parser.IsEnum(lexeme)) return ParseEnum();

            if (_parser.IsService(lexeme)) return ParseService();

            if (_parser.IsMessage(lexeme)) return ParseMessage();

            _errors.Add(new ParseError("Found an invalid top level statement at token ", token));
            return null;
        }

        private Node ParseInlineComment()
        {
            var token = _tokens.Dequeue(); // Remove the line comment token

            var commentNode = new Node(NodeType.Comment, token.Lexeme);
            var commentText = new Node(NodeType.CommentText, DumpStringToEndLine());
            commentNode.AddChild(commentText);
            return commentNode;
        }

        private Node ParseMultiLineComment()
        {
            var token = _tokens.Dequeue(); // Remove the open block comment token
            var commentNode = new Node(NodeType.Comment, token.Lexeme);
            var commentText = new Node(NodeType.CommentText, CollectBlockComment());
            commentNode.AddChild(commentText);
            return commentNode;
        }

        private Node ParseSyntax()
        {
            var syntax = _tokens.Dequeue(); // Pop off syntax token.

            var assignment = _tokens.Dequeue();
            if (!_parser.IsAssignment(assignment.Lexeme))
            {
                _errors.Add(new ParseError("Expected an assignment after syntax token, found ", assignment));
                return null;
            }
            var proto3 = ParseStringLiteral();

            if (ReferenceEquals(proto3, null))
            {
                _errors.Add(new ParseError("Expected a string literal after syntax assignment, found token ", _tokens.Peek()));
                return null;
            }

            TerminateSingleLineStatement();

            var syntaxNode = new Node(NodeType.Syntax, syntax.Lexeme);
            syntaxNode.AddChild(proto3);

            ScoopComment(syntaxNode);
            DumpEndline();

            return syntaxNode;
        }

        private Node ParseImport()
        {
            var importTag = _tokens.Dequeue();

            var modifier = ParseImportModifier();

            var importValue = ParseStringLiteral();

            if (ReferenceEquals(importValue, null))
            {
                _errors.Add(new ParseError("Could not find import location for import at line starting with token ", importTag));
                return null;
            }

            TerminateSingleLineStatement();

            var importNode = new Node(NodeType.Import, importTag.Lexeme);
            importNode.AddChild(modifier);
            importNode.AddChild(importValue);

            ScoopComment(importNode);
            DumpEndline();

            return importNode;
        }

        private void TerminateSingleLineStatement()
        {
            var terminator = _tokens.Dequeue();
            if (!_parser.IsEmptyStatement(terminator.Lexeme))
            {
                _errors.Add(new ParseError("Expected terminating `;` after syntax declaration, found token ", terminator));
            }
        }

        private void ScoopComment(Node parent)
        {
            var trailing = _tokens.Peek();
            if (!_parser.IsInlineComment(trailing.Lexeme)) return;
            var commentNode = ParseInlineComment();
            parent.AddChild(commentNode);
        }

        private void DumpEndline()
        {
            if (!_tokens.Any()) return;
            var trailing = _tokens.Peek();
            if (trailing.Type.Equals(TokenType.EndLine)) _tokens.Dequeue(); // Dump the endline
        }

        private Node ParseImportModifier()
        {
            var mod = _tokens.Peek();
            if (!_parser.IsImportModifier(mod.Lexeme)) return null;
            _tokens.Dequeue();
            return new Node(NodeType.ImportModifier, mod.Lexeme);
        }

        private Node ParseStringLiteral()
        {
            var stringLit = _tokens.Peek();
            if (!_parser.IsStringLiteral(stringLit.Lexeme)) return null;
            _tokens.Dequeue();
            return new Node(NodeType.StringLiteral, stringLit.Lexeme.Unquote());
        }

        private Node ParseIdentifier()
        {
            var ident = _tokens.Peek();
            if (!_parser.IsIdentifier(ident.Lexeme)) return null;
            _tokens.Dequeue();
            return new Node(NodeType.Identifier, ident.Lexeme);
        }

        private Node ParseFullIdentifier()
        {
            var ident = _tokens.Peek();
            if (!_parser.IsFullIdentifier(ident.Lexeme)) return null;
            _tokens.Dequeue();
            return new Node(NodeType.Identifier, ident.Lexeme);
        }

        private Node ParsePackage()
        {
            var packageTag = _tokens.Dequeue();

            var packageName = ParseFullIdentifier();

            if (ReferenceEquals(packageName, null))
            {
                _errors.Add(new ParseError($"Could not find a package name for package starting at line {packageTag.Line} Found token ", _tokens.Peek()));
                return null;
            }

            TerminateSingleLineStatement();

            var packageNode = new Node(NodeType.Package, packageTag.Lexeme);
            packageNode.AddChild(packageName);

            ScoopComment(packageNode);
            DumpEndline();

            return packageNode;
        }

        private Node ParseOption()
        {
            var optionTag = _tokens.Dequeue();

            var optionName = ParseFullIdentifier();
            if (ReferenceEquals(optionName, null))
            {
                _errors.Add(
                    new ParseError(
                        $"Could not find an option name for option starting at line {optionTag.Line} Found token ",
                        _tokens.Peek()));
            }

            var assignment = _tokens.Dequeue();
            if (!_parser.IsAssignment(assignment.Lexeme))
            {
                _errors.Add(new ParseError($"Expected an assignment after option name token on line {optionTag.Line}, found ", assignment));
                return null;
            }

            var optionValue = ParseStringLiteral();
            if (ReferenceEquals(optionValue, null))
            {
                _errors.Add(
                    new ParseError(
                        $"Could not find an option value for option starting at line {optionTag.Line} Found token ",
                        _tokens.Peek()));
                return null;
            }

            TerminateSingleLineStatement();

            var optionNode = new Node(NodeType.Option, optionTag.Lexeme);
            optionNode.AddChild(optionName);
            optionNode.AddChild(optionValue);

            ScoopComment(optionNode);
            DumpEndline();

            return optionNode;
        }

        private Node ParseEnum()
        {
            var enumTag = _tokens.Peek();
            if (!enumTag.Lexeme.Equals("enum")) return null;
            _tokens.Dequeue();

            var enumNode = new Node(NodeType.Enum, enumTag.Lexeme);

            var msgName = ParseIdentifier();
            if (ReferenceEquals(msgName, null))
            {
                _errors.Add(
                    new ParseError(
                        $"Could not find a message name on line {enumTag.Line} for message token ",
                        enumTag));
                return null;
            }

            enumNode.AddChild(msgName);

            var openBrack = _tokens.Dequeue();
            if (!openBrack.Type.Equals(TokenType.Control) || !openBrack.Lexeme[0].Equals('{'))
            {
                _errors.Add(
                    new ParseError(
                        $"Expected to find open bracket on line {enumTag.Line} for message token ",
                        enumTag));
                return null;
            }

            ScoopComment(enumNode);
            DumpEndline();

            var next = _tokens.Peek();
            while (!next.Type.Equals(TokenType.Control) && !next.Lexeme[0].Equals('}'))
            {
                var fieldNode = ParseEnumField();
                enumNode.AddChild(fieldNode);
                if (_tokens.Any()) next = _tokens.Peek();
            }

            _tokens.Dequeue();
            DumpEndline();

            return enumNode;
        }

        private Node ParseEnumField()
        {
            if (!_tokens.Any()) return null;
            var openToken = _tokens.Peek();
            var fieldNode = new Node(NodeType.EnumField, openToken.Lexeme);

            var name = ParseIdentifier();
            fieldNode.AddChild(name);

            var assignment = _tokens.Dequeue();
            if (!_parser.IsAssignment(assignment.Lexeme))
            {
                _errors.Add(new ParseError($"Expected an assignment after enum field name token on line {openToken.Line}, found ", assignment));
                return null;
            }

            var fieldValue = _tokens.Dequeue();
            if (!_parser.IsIntegerLiteral(fieldValue.Lexeme))
            {
                _errors.Add(new ParseError($"Expected a field value after assignment token on line {openToken.Line}, found ", fieldValue));
                return null;
            }
            fieldNode.AddChild(new Node(NodeType.FieldNumber, fieldValue.Lexeme));

            TerminateSingleLineStatement();
            ScoopComment(fieldNode);
            DumpEndline();

            return fieldNode;
        }

        private Node ParseService()
        {
            return null;
        }

        private Node ParseMessage()
        {
            if (!_tokens.Any()) return null;
            var msgTag = _tokens.Peek();
            if (!msgTag.Lexeme.Equals("message")) return null;
            _tokens.Dequeue();

            var msgNode = new Node(NodeType.Message, msgTag.Lexeme);

            var msgName = ParseIdentifier();
            if (ReferenceEquals(msgName, null))
            {
                _errors.Add(
                    new ParseError(
                        $"Could not find a message name on line {msgTag.Line} for message token ",
                        msgTag));
                return null;
            }

            msgNode.AddChild(msgName);

            var openBrack = _tokens.Dequeue();
            if (!openBrack.Type.Equals(TokenType.Control) || !openBrack.Lexeme[0].Equals('{'))
            {
                _errors.Add(
                    new ParseError(
                        $"Expected to find open bracket on line {msgTag.Line} for message token ",
                        msgTag));
                return null;
            }

            ScoopComment(msgNode);
            DumpEndline();

            var next = _tokens.Peek();
            while (!next.Type.Equals(TokenType.Control) && !next.Lexeme[0].Equals('}'))
            {
                // Some of these may be null returns. That's ok. AddChildren will ignore.
                var reservation = ParseReservation();
                var fieldNode = ParseMessageField();
                var nestedMessage = ParseMessage();
                var nestedEnum = ParseEnum();
                var oneOf = ParseOneOfField();
                var map = ParseMapField();

                msgNode.AddChildren(reservation, fieldNode, nestedMessage, nestedEnum, oneOf, map);

                if (_tokens.Any()) next = _tokens.Peek();
            }

            _tokens.Dequeue(); // Dump the }
            DumpEndline();

            return msgNode;
        }

        private Node ParseReservation()
        {
            var reserveTag = _tokens.Peek();
            if (!reserveTag.Lexeme.Equals("reserved")) return null;
            _tokens.Dequeue();

            var reservedNode = new Node(NodeType.Reserved, reserveTag.Lexeme);

            var next = _tokens.Peek();
            if (_parser.IsStringLiteral(next.Lexeme))
            {
                var nameRange = ParseStringRange().ToArray();

                if (nameRange.Any())
                {
                    reservedNode.AddChildren(nameRange);
                    TerminateSingleLineStatement();
                    ScoopComment(reservedNode);
                    DumpEndline();
                    return reservedNode;
                }
            }

            if (_parser.IsDecimalLiteral(next.Lexeme))
            {
                var intRange = ParseIntegerRange().ToArray();

                if (intRange.Any())
                {
                    reservedNode.AddChildren(intRange);
                    TerminateSingleLineStatement();
                    ScoopComment(reservedNode);
                    DumpEndline();
                    return reservedNode;
                }
            }

            var existingString = DumpStringToEndLine();
            _errors.Add(
                    new ParseError(
                        $"Could not find a valid reservation on line {reserveTag.Line}. Found: {existingString}",
                        reserveTag));
            return null;
        }

        private IEnumerable<Node> ParseIntegerRange()
        {
            var intRes = new Stack<int>();
            var token = _tokens.Peek();
            while (!token.Lexeme.Equals(";"))
            {
                token = _tokens.Dequeue();
                var lexeme = token.Lexeme;
                if (",".Equals(lexeme))
                {
                    if (!intRes.Any())
                    {
                        _errors.Add(
                            new ParseError(
                                "Expected integer literal before ',' in reserved range ",
                                token));
                        return new List<Node>();
                    }
                    token = _tokens.Peek();
                    continue;
                }

                if (_parser.IsDecimalLiteral(lexeme))
                {
                    intRes.Push(int.Parse(lexeme));
                    token = _tokens.Peek();
                    continue;
                }

                if (!"to".Equals(lexeme))
                {
                    token = _tokens.Peek();
                    continue;
                }

                // So now we are looking ahead at the token after 'to', so we have something like '9 to 11'
                if (!intRes.Any()) // In the case that we found a 'to' but haven't yet found an integer
                {
                    _errors.Add(
                       new ParseError(
                           "Expected integer literal before 'to' in reserved range ",
                           token));
                    return new List<Node>();
                }

                var startRangeAt = intRes.Pop(); // Go get the last integer read, e.g. 9
                var nextToken = _tokens.Peek(); // Look ahead for the next integer, e.g. 11
                if (!_parser.IsDecimalLiteral(nextToken.Lexeme)) // If the next token isn't an integer create Error
                {
                    _errors.Add(
                        new ParseError(
                            "Expected integer literal after 'to' in reserved range ",
                            nextToken));
                    return new List<Node>();
                }

                // If we don't have an error go ahead and remove the token and use it to find the end range.
                nextToken = _tokens.Dequeue();
                var endRangeAt = int.Parse(nextToken.Lexeme);

                // Now push all the integers in the range onto the stack.
                var rangeLength = endRangeAt - startRangeAt + 1;
                foreach (var elem in Enumerable.Range(startRangeAt, rangeLength))
                {
                    intRes.Push(elem);
                }

                // If we've got this far, set the token for the While comparison to the next.
                token = _tokens.Peek();
            }

            // Now that we've hit an Endline or ';' terminator, return.
            return intRes.Select(t => new Node(NodeType.IntegerLiteral, t.ToString())).Reverse();
        }

        private IEnumerable<Node> ParseStringRange()
        {
            var stringRes = new List<string>();
            var token = _tokens.Peek();
            while (!token.Lexeme.Equals(";"))
            {
                token = _tokens.Dequeue();
                var lexeme = token.Lexeme;
                if (",".Equals(lexeme))
                {
                    token = _tokens.Peek();
                    continue;
                }
                if (!_parser.IsStringLiteral(lexeme))
                {
                    if (!stringRes.Any())
                    {
                        _errors.Add(
                            new ParseError(
                                "Expected string literal before ',' in reserved range ",
                                token));
                        return new List<Node>();
                    }
                    token = _tokens.Peek();
                    continue;
                }
                stringRes.Add(lexeme.Unquote());
                token = _tokens.Peek();
            }
            return stringRes.Select(t => new Node(NodeType.StringLiteral, t));
        }

        private Node ParseOneOfField()
        {
            var oneOfTag = _tokens.Peek();
            if (!oneOfTag.Lexeme.Equals("oneof")) return null;
            _tokens.Dequeue();

            var msgNode = new Node(NodeType.OneOfField, oneOfTag.Lexeme);

            var msgName = ParseIdentifier();
            if (ReferenceEquals(msgName, null))
            {
                _errors.Add(
                    new ParseError(
                        $"Could not find a oneof name on line {oneOfTag.Line} for message token ",
                        oneOfTag));
                return null;
            }

            var openBrack = _tokens.Dequeue();
            if (!openBrack.Type.Equals(TokenType.Control) || !openBrack.Lexeme[0].Equals('{'))
            {
                _errors.Add(
                    new ParseError(
                        $"Expected to find open bracket on line {oneOfTag.Line} for message token ",
                        oneOfTag));
                return null;
            }

            msgNode.AddChild(msgName);

            ScoopComment(msgNode);
            DumpEndline();

            var next = _tokens.Peek();
            while (!next.Type.Equals(TokenType.Control) && !next.Lexeme[0].Equals('}'))
            {
                // Some of these may be null returns. That's ok. AddChildren will ignore.
                var fieldNode = ParseMessageField();
                var oneOf = ParseOneOfField();

                msgNode.AddChildren(fieldNode, oneOf);

                if (_tokens.Any()) next = _tokens.Peek();
            }

            _tokens.Dequeue(); // Dump the }
            DumpEndline();

            return msgNode;
        }

        private Node ParseMapField()
        {
            var mapTag = _tokens.Peek();
            if (!mapTag.Lexeme.Equals("map")) return null;
            _tokens.Dequeue();

            var mapNode = new Node(NodeType.Map, mapTag.Lexeme);

            var openAngle = _tokens.Dequeue();
            if (!"<".Equals(openAngle.Lexeme))
            {
                _errors.Add(
                   new ParseError(
                       $"Expected open angle bracket at column {openAngle.Column} on line {openAngle.Line} for map definition ",
                       mapTag));
                return null;
            }

            var key = _tokens.Dequeue();
            if (!key.Lexeme.IsMapKeyType())
            {
                _errors.Add(
                   new ParseError(
                       $"Expected valid map key type at column {key.Column} on line {key.Line} for map definition. Found {key.Lexeme} ",
                       mapTag));
                return null;
            }
            var mapKey = new Node(NodeType.MapKey, key.Lexeme);

            var comma = _tokens.Dequeue();
            if (!",".Equals(comma.Lexeme))
            {
                _errors.Add(
                   new ParseError(
                       $"Expected open comma at column {comma.Column} on line {comma.Line} for map definition ",
                       mapTag));
                return null;
            }

            var value = ParseMapValueType();

            if (ReferenceEquals(null, value))
            {
                _errors.Add(
                    new ParseError(
                        "Expected a user or value type for map definition ",
                        mapTag));
                return null;
            }

            var closeAngle = _tokens.Dequeue();
            if (!">".Equals(closeAngle.Lexeme))
            {
                _errors.Add(
                   new ParseError(
                       $"Expected close angle bracket at column {closeAngle.Column} on line {closeAngle.Line} for map definition ",
                       mapTag));
                return null;
            }

            var msgName = ParseIdentifier();
            if (ReferenceEquals(msgName, null))
            {
                _errors.Add(
                    new ParseError(
                        $"Could not find a oneof name on line {mapTag.Line} for message token ",
                        mapTag));
                return null;
            }

            var assignment = _tokens.Dequeue();
            if (!_parser.IsAssignment(assignment.Lexeme))
            {
                _errors.Add(new ParseError($"Expected an assignment after field name token on line {mapTag.Line}, found ", assignment));
                return null;
            }

            var fieldValue = _tokens.Dequeue();
            if (!_parser.IsIntegerLiteral(fieldValue.Lexeme))
            {
                _errors.Add(new ParseError($"Expected a field value after assignment token on line {mapTag.Line}, found ", fieldValue));
                return null;
            }
            var fieldNumber = new Node(NodeType.FieldNumber, fieldValue.Lexeme);

            mapNode.AddChildren(msgName, mapKey, value, fieldNumber);

            TerminateSingleLineStatement();
            ScoopComment(mapNode);
            DumpEndline();

            return mapNode;
        }

        private Node ParseRepeated()
        {
            var repeated = _tokens.Peek();
            if (!_parser.IsRepeated(repeated.Lexeme)) return null;

            _tokens.Dequeue();
            return new Node(NodeType.Repeated, repeated.Lexeme);
        }

        private Node ParseMapValueType()
        {
            var type = _tokens.Peek();
            if (!_parser.IsBasicType(type.Lexeme) && !_parser.IsFullIdentifier(type.Lexeme)) return null;

            _tokens.Dequeue();
            return new Node(NodeType.MapValue, type.Lexeme);
        }

        private Node ParseBasicType()
        {
            var type = _tokens.Peek();
            if (!_parser.IsBasicType(type.Lexeme)) return null;

            _tokens.Dequeue();
            return new Node(NodeType.Type, type.Lexeme);
        }

        private Node ParseUserType()
        {
            var usertype = _tokens.Peek();
            if (!_parser.IsFullIdentifier(usertype.Lexeme)) return null;

            _tokens.Dequeue();
            return new Node(NodeType.UserType, usertype.Lexeme);
        }

        private Node ParseMessageField()
        {
            if (!_tokens.Any()) return null;
            var openToken = _tokens.Peek();
            var lexeme = openToken.Lexeme;
            if (!_parser.IsFieldStart(lexeme)) return null;
            var fieldNode = new Node(NodeType.Field, openToken.Lexeme);

            var repeated = ParseRepeated();
            fieldNode.AddChild(repeated);

            var basicType = ParseBasicType();
            fieldNode.AddChild(basicType);

            if (ReferenceEquals(basicType, null))
            {
                fieldNode.AddChild(ParseUserType());
            }

            var name = ParseIdentifier();
            fieldNode.AddChild(name);

            var assignment = _tokens.Dequeue();
            if (!_parser.IsAssignment(assignment.Lexeme))
            {
                _errors.Add(new ParseError($"Expected an assignment after field name token on line {openToken.Line}, found ", assignment));
                return null;
            }

            var fieldValue = _tokens.Dequeue();
            if (!_parser.IsIntegerLiteral(fieldValue.Lexeme))
            {
                _errors.Add(new ParseError($"Expected a field value after assignment token on line {openToken.Line}, found ", fieldValue));
                return null;
            }
            fieldNode.AddChild(new Node(NodeType.FieldNumber, fieldValue.Lexeme));

            TerminateSingleLineStatement();
            ScoopComment(fieldNode);
            DumpEndline();

            return fieldNode;
        }

        private string DumpStringToEndLine()
        {
            Token token;
            var buffer = new List<Token>();
            do
            {
                token = _tokens.Dequeue();
                buffer.Add(token);
                token = _tokens.Peek();
            } while (!token.Type.Equals(TokenType.EndLine));

            if (_tokens.Any() && _tokens.Peek().Type.Equals(TokenType.EndLine))
                _tokens.Dequeue(); // Dump the trailing EOL

            return string.Join(" ", buffer.Select(l => l.Lexeme));
        }

        private string CollectBlockComment()
        {
            var foundClosing = false;
            var buffer = new List<Token>();
            do
            {
                var token = _tokens.Dequeue();
                if (_parser.IsMultiLineCommentClose(token.Lexeme))
                {
                    foundClosing = true;
                }
                else
                {
                    buffer.Add(token);
                }
            } while (!foundClosing);

            if (_tokens.Any() && _tokens.Peek().Type.Equals(TokenType.EndLine))
                _tokens.Dequeue(); // Dump the trailing EOL

            return string.Join(" ", buffer.Select(token =>
            {
                if (token.Type.Equals(TokenType.String)) return token.Lexeme;
                return token.Type.Equals(TokenType.EndLine) ? Environment.NewLine : string.Empty;
            }));
        }
    }
}