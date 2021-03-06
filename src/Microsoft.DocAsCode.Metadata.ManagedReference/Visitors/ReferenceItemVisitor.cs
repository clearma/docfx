// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    using System.Collections.Generic;

    using Microsoft.CodeAnalysis;

    using Microsoft.DocAsCode.DataContracts.ManagedReference;

    public abstract class ReferenceItemVisitor : SymbolVisitor
    {
        public static readonly SymbolDisplayFormat ShortFormat = SimpleYamlModelGenerator.ShortFormat;
        public static readonly SymbolDisplayFormat QualifiedFormat = SimpleYamlModelGenerator.QualifiedFormat;
        public static readonly SymbolDisplayFormat ShortFormatWithoutGenericeParameter = ShortFormat
            .WithGenericsOptions(SymbolDisplayGenericsOptions.None);
        public static readonly SymbolDisplayFormat QualifiedFormatWithoutGenericeParameter = QualifiedFormat
            .WithGenericsOptions(SymbolDisplayGenericsOptions.None);

        protected ReferenceItemVisitor(ReferenceItem referenceItem)
        {
            ReferenceItem = referenceItem;
        }

        protected ReferenceItem ReferenceItem { get; private set; }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.IsGenericType)
            {
                if (symbol.IsUnboundGenericType)
                {
                    AddLinkItems(symbol, true);
                }
                else
                {
                    AddLinkItems(symbol.OriginalDefinition, false);
                    AddBeginGenericParameter();
                    for (int i = 0; i < symbol.TypeArguments.Length; i++)
                    {
                        if (i > 0)
                        {
                            AddGenericParameterSeparator();
                        }
                        symbol.TypeArguments[i].Accept(this);
                    }
                    AddEndGenericParameter();
                }
            }
            else
            {
                AddLinkItems(symbol, true);
            }
        }

        protected abstract void AddLinkItems(INamedTypeSymbol symbol, bool withGenericeParameter);

        protected abstract void AddBeginGenericParameter();

        protected abstract void AddGenericParameterSeparator();

        protected abstract void AddEndGenericParameter();
    }

    public class CSReferenceItemVisitor
        : ReferenceItemVisitor
    {
        public CSReferenceItemVisitor(ReferenceItem referenceItem) : base(referenceItem)
        {
            if (!referenceItem.Parts.ContainsKey(SyntaxLanguage.CSharp))
            {
                referenceItem.Parts.Add(SyntaxLanguage.CSharp, new List<LinkItem>());
            }
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetCSharp(NameOptions.None).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetCSharp(NameOptions.Qualified).GetName(symbol),
            });
        }

        public override void VisitTypeParameter(ITypeParameterSymbol symbol)
        {
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = symbol.Name,
                DisplayQualifiedNames = symbol.Name,
            });
        }

        public override void VisitArrayType(IArrayTypeSymbol symbol)
        {
            symbol.ElementType.Accept(this);
            if (symbol.Rank == 1)
            {
                ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                {
                    DisplayName = "[]",
                    DisplayQualifiedNames = "[]",
                });
            }
            else
            {
                ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                {
                    DisplayName = "[" + new string(',', symbol.Rank - 1) + "]",
                    DisplayQualifiedNames = "[" + new string(',', symbol.Rank - 1) + "]",
                });
            }
        }

        public override void VisitPointerType(IPointerTypeSymbol symbol)
        {
            symbol.PointedAtType.Accept(this);
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = "*",
                DisplayQualifiedNames = "*",
            });
        }

        public override void VisitMethod(IMethodSymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetCSharp(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetCSharp(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = "(",
                DisplayQualifiedNames = "(",
            });
            for (int i = 0; i < symbol.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                    {
                        DisplayName = ", ",
                        DisplayQualifiedNames = ", ",
                    });
                }
                symbol.Parameters[i].Type.Accept(this);
            }
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = ")",
                DisplayQualifiedNames = ")",
            });
        }

        public override void VisitProperty(IPropertySymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetCSharp(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetCSharp(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
            if (symbol.Parameters.Length > 0)
            {

                ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                {
                    DisplayName = "[",
                    DisplayQualifiedNames = "[",
                });
                for (int i = 0; i < symbol.Parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                        {
                            DisplayName = ", ",
                            DisplayQualifiedNames = ", ",
                        });
                    }
                    symbol.Parameters[i].Type.Accept(this);
                }
                ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                {
                    DisplayName = "]",
                    DisplayQualifiedNames = "]",
                });
            }
        }

        public override void VisitEvent(IEventSymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetCSharp(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetCSharp(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
        }

        public override void VisitField(IFieldSymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetCSharp(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetCSharp(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
        }

        protected override void AddBeginGenericParameter()
        {
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = "<",
                DisplayQualifiedNames = "<",
            });
        }

        protected override void AddEndGenericParameter()
        {
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = ">",
                DisplayQualifiedNames = ">",
            });
        }

        protected override void AddGenericParameterSeparator()
        {
            ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
            {
                DisplayName = ", ",
                DisplayQualifiedNames = ", ",
            });
        }

        protected override void AddLinkItems(INamedTypeSymbol symbol, bool withGenericeParameter)
        {
            var id = VisitorHelper.GetId(symbol);
            if (withGenericeParameter)
            {
                ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                {
                    DisplayName = NameVisitorCreator.GetCSharp(NameOptions.WithGenericParameter).GetName(symbol),
                    DisplayQualifiedNames = NameVisitorCreator.GetCSharp(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                    Name = id,
                    IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
                });
            }
            else
            {
                ReferenceItem.Parts[SyntaxLanguage.CSharp].Add(new LinkItem
                {
                    DisplayName = NameVisitorCreator.GetCSharp(NameOptions.None).GetName(symbol),
                    DisplayQualifiedNames = NameVisitorCreator.GetCSharp(NameOptions.Qualified).GetName(symbol),
                    Name = id,
                    IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
                });
            }
        }
    }

    public class VBReferenceItemVisitor
        : ReferenceItemVisitor
    {
        public VBReferenceItemVisitor(ReferenceItem referenceItem) : base(referenceItem)
        {
            if (!referenceItem.Parts.ContainsKey(SyntaxLanguage.VB))
            {
                referenceItem.Parts.Add(SyntaxLanguage.VB, new List<LinkItem>());
            }
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetVB(NameOptions.None).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetVB(NameOptions.Qualified).GetName(symbol),
            });
        }

        public override void VisitTypeParameter(ITypeParameterSymbol symbol)
        {
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = symbol.Name,
                DisplayQualifiedNames = symbol.Name,
            });
        }

        public override void VisitArrayType(IArrayTypeSymbol symbol)
        {
            symbol.ElementType.Accept(this);
            if (symbol.Rank == 1)
            {
                ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                {
                    DisplayName = "()",
                    DisplayQualifiedNames = "()",
                });
            }
            else
            {
                ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                {
                    DisplayName = "(" + new string(',', symbol.Rank - 1) + ")",
                    DisplayQualifiedNames = "(" + new string(',', symbol.Rank - 1) + ")",
                });
            }
        }

        public override void VisitPointerType(IPointerTypeSymbol symbol)
        {
            symbol.PointedAtType.Accept(this);
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = "*",
                DisplayQualifiedNames = "*",
            });
        }

        public override void VisitMethod(IMethodSymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetVB(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetVB(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = "(",
                DisplayQualifiedNames = "(",
            });
            for (int i = 0; i < symbol.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                    {
                        DisplayName = ", ",
                        DisplayQualifiedNames = ", ",
                    });
                }
                symbol.Parameters[i].Type.Accept(this);
            }
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = ")",
                DisplayQualifiedNames = ")",
            });
        }

        public override void VisitProperty(IPropertySymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetVB(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetVB(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
            if (symbol.Parameters.Length > 0)
            {
                ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                {
                    DisplayName = "(",
                    DisplayQualifiedNames = "(",
                });
                for (int i = 0; i < symbol.Parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                        {
                            DisplayName = ", ",
                            DisplayQualifiedNames = ", ",
                        });
                    }
                    symbol.Parameters[i].Type.Accept(this);
                }
                ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                {
                    DisplayName = ")",
                    DisplayQualifiedNames = ")",
                });
            }
        }

        public override void VisitEvent(IEventSymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetVB(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetVB(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
        }

        public override void VisitField(IFieldSymbol symbol)
        {
            var id = VisitorHelper.GetId(symbol.OriginalDefinition);
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = NameVisitorCreator.GetVB(NameOptions.WithGenericParameter).GetName(symbol),
                DisplayQualifiedNames = NameVisitorCreator.GetVB(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                Name = id,
                IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
            });
        }

        protected override void AddBeginGenericParameter()
        {
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = "(Of ",
                DisplayQualifiedNames = "(Of ",
            });
        }

        protected override void AddEndGenericParameter()
        {
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = ")",
                DisplayQualifiedNames = ")",
            });
        }

        protected override void AddGenericParameterSeparator()
        {
            ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
            {
                DisplayName = ", ",
                DisplayQualifiedNames = ", ",
            });
        }

        protected override void AddLinkItems(INamedTypeSymbol symbol, bool withGenericeParameter)
        {
            var id = VisitorHelper.GetId(symbol);
            if (withGenericeParameter)
            {
                ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                {
                    DisplayName = NameVisitorCreator.GetVB(NameOptions.WithGenericParameter).GetName(symbol),
                    DisplayQualifiedNames = NameVisitorCreator.GetVB(NameOptions.Qualified | NameOptions.WithGenericParameter).GetName(symbol),
                    Name = id,
                    IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
                });
            }
            else
            {
                ReferenceItem.Parts[SyntaxLanguage.VB].Add(new LinkItem
                {
                    DisplayName = NameVisitorCreator.GetVB(NameOptions.None).GetName(symbol),
                    DisplayQualifiedNames = NameVisitorCreator.GetVB(NameOptions.Qualified).GetName(symbol),
                    Name = id,
                    IsExternalPath = symbol.IsExtern || symbol.DeclaringSyntaxReferences.Length == 0,
                });
            }
        }
    }
}
