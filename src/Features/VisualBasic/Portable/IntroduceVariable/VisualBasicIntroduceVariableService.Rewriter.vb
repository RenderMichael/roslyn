﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.IntroduceVariable
    Partial Friend Class VisualBasicIntroduceVariableService

        Private Class Rewriter
            Inherits VisualBasicSyntaxRewriter

            Private Shared ReadOnly s_replacementAnnotation As New SyntaxAnnotation

            Private ReadOnly _replacementNode As SyntaxNode
            Private ReadOnly _matches As ISet(Of ExpressionSyntax)

            Private Sub New(replacementNode As SyntaxNode, matches As ISet(Of ExpressionSyntax))
                _replacementNode = replacementNode
                _matches = matches
            End Sub

            Public Overrides Function Visit(node As SyntaxNode) As SyntaxNode
                Dim expression = TryCast(node, ExpressionSyntax)
                If expression IsNot Nothing AndAlso _matches.Contains(expression) Then
                    Return _replacementNode.
                        WithLeadingTrivia(expression.GetLeadingTrivia()).
                        WithTrailingTrivia(expression.GetTrailingTrivia()).
                        WithAdditionalAnnotations(s_replacementAnnotation)
                End If

                Return MyBase.Visit(node)
            End Function

            Public Overrides Function VisitParenthesizedExpression(node As ParenthesizedExpressionSyntax) As SyntaxNode
                Dim newNode = MyBase.VisitParenthesizedExpression(node)
                If node IsNot newNode AndAlso newNode.IsKind(SyntaxKind.ParenthesizedExpression) Then
                    Dim parenthesizedExpression = DirectCast(newNode, ParenthesizedExpressionSyntax)
                    Dim innerExpression = parenthesizedExpression.OpenParenToken.GetNextToken().Parent
                    If innerExpression.HasAnnotation(s_replacementAnnotation) AndAlso innerExpression.Equals(parenthesizedExpression.Expression) Then
                        Return newNode.WithAdditionalAnnotations(Simplifier.Annotation)
                    End If
                End If

                Return newNode
            End Function

            Public Overloads Shared Function Visit(node As SyntaxNode, replacementNode As SyntaxNode, matches As ISet(Of ExpressionSyntax)) As SyntaxNode
                Return New Rewriter(replacementNode, matches).Visit(node)
            End Function

            Public Overrides Function VisitInferredFieldInitializer(node As InferredFieldInitializerSyntax) As SyntaxNode
                Dim newNode = DirectCast(MyBase.VisitInferredFieldInitializer(node), InferredFieldInitializerSyntax)
                If newNode IsNot node AndAlso
                   newNode.Expression.HasAnnotation(s_replacementAnnotation) Then

                    Dim inferredName = node.Expression.TryGetInferredMemberName()
                    If inferredName IsNot Nothing Then
                        Return SyntaxFactory.NamedFieldInitializer(
                            SyntaxFactory.IdentifierName(inferredName.EscapeIdentifier(afterDot:=True)),
                            newNode.Expression.WithoutLeadingTrivia()).WithTriviaFrom(newNode)
                    End If
                End If

                Return newNode
            End Function

            Public Overrides Function VisitSimpleArgument(node As SimpleArgumentSyntax) As SyntaxNode
                Dim newNode = DirectCast(MyBase.VisitSimpleArgument(node), SimpleArgumentSyntax)
                If newNode IsNot node AndAlso
                   node.NameColonEquals Is Nothing AndAlso
                   newNode.Expression.HasAnnotation(s_replacementAnnotation) AndAlso
                   TypeOf node.Parent Is TupleExpressionSyntax Then

                    Dim inferredName = node.Expression.TryGetInferredMemberName()
                    If inferredName IsNot Nothing Then
                        Return SyntaxFactory.SimpleArgument(
                            SyntaxFactory.NameColonEquals(SyntaxFactory.IdentifierName(inferredName.EscapeIdentifier())),
                            newNode.Expression.WithoutLeadingTrivia()).WithTriviaFrom(newNode)
                    End If
                End If

                Return newNode
            End Function
        End Class
    End Class
End Namespace
