using FluentAssertions;
using UMLForge.Contracts.Grpc;
using UMLForge.DiagramEngine.Grpc.Validators;
using UMLForge.DiagramEngine.Grpc.Models;
using Xunit;

namespace UMLForge.DiagramEngine.Tests;

public class UmlRuleValidatorTests
{
    private readonly UmlRuleValidator _validator = new();
    private readonly RelationSuggester _suggester = new();

    [Fact]
    public void ValidateDiagram_ValidUseCase_ShouldReturnValid()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = """
            {
              "type": "UseCase",
              "title": "Hospital Management",
              "systemBoundary": "Hospital",
              "actors": ["Doctor", "Patient"],
              "useCases": ["Schedule Appointment"],
              "relations": [
                { "from": "Patient", "to": "Schedule Appointment" }
              ]
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDiagram_UseCaseWithoutActors_ShouldReturnInvalidWithSpecificError()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = """
            {
              "type": "UseCase",
              "title": "Hospital Management",
              "systemBoundary": "Hospital",
              "actors": [],
              "useCases": ["Schedule Appointment"],
              "relations": []
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least one actor"));
    }

    [Fact]
    public void ValidateDiagram_SequenceWithLessThanTwoParticipants_ShouldReturnInvalid()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.Sequence,
            ModelJson = """
            {
              "type": "Sequence",
              "title": "Single Actor Flow",
              "participants": ["SoloService"],
              "messages": []
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least two participants"));
    }

    [Fact]
    public void SuggestRelations_UnconnectedActor_ShouldSuggestConnectionToFirstUseCase()
    {
        // Arrange
        var request = new RelationSuggestionRequest
        {
            DiagramType = (int)DiagramType.UseCase,
            ModelJson = """
            {
              "type": "UseCase",
              "title": "Course Registration",
              "actors": ["Student", "Auditor"],
              "useCases": ["Browse Catalog"],
              "relations": [
                { "from": "Student", "to": "Browse Catalog" }
              ]
            }
            """
        };

        // Act
        var result = _suggester.Suggest(request);

        // Assert
        result.Suggestions.Should().HaveCount(1);
        result.Suggestions[0].From.Should().Be("Auditor");
        result.Suggestions[0].To.Should().Be("Browse Catalog");
        result.Suggestions[0].Description.Should().Contain("unconnected");
    }

    [Fact]
    public void ValidateDiagram_SequenceWithValidAltFragment_ShouldReturnValid()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.Sequence,
            ModelJson = """
            {
              "title": "Pay",
              "participants": ["App", "VNPay"],
              "messages": [
                { "from": "App", "to": "VNPay", "label": "pay" },
                { "fragment": "alt", "branches": [
                  { "condition": "success", "messages": [ { "from": "VNPay", "to": "App", "label": "paid", "isResponse": true } ] },
                  { "condition": "failed",  "messages": [ { "from": "VNPay", "to": "App", "label": "error", "isResponse": true } ] }
                ] }
              ]
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDiagram_SequenceFragmentWithUndeclaredParticipant_ShouldReturnInvalid()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.Sequence,
            ModelJson = """
            {
              "title": "Pay",
              "participants": ["App", "VNPay"],
              "messages": [
                { "fragment": "opt", "branches": [
                  { "condition": "has voucher", "messages": [ { "from": "App", "to": "VoucherService", "label": "apply" } ] }
                ] }
              ]
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'VoucherService' is not in declared participants"));
    }

    [Fact]
    public void ValidateDiagram_SequenceUnsupportedFragment_ShouldReturnInvalid()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.Sequence,
            ModelJson = """
            {
              "title": "Pay",
              "participants": ["App", "VNPay"],
              "messages": [
                { "fragment": "par", "branches": [
                  { "condition": "x", "messages": [ { "from": "App", "to": "VNPay", "label": "pay" } ] }
                ] }
              ]
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("fragment 'par' is not supported"));
    }

    [Fact]
    public void ValidateDiagram_SequenceOptWithTwoBranches_ShouldReturnInvalid()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.Sequence,
            ModelJson = """
            {
              "title": "Pay",
              "participants": ["App", "VNPay"],
              "messages": [
                { "fragment": "opt", "branches": [
                  { "condition": "a", "messages": [ { "from": "App", "to": "VNPay", "label": "x" } ] },
                  { "condition": "b", "messages": [ { "from": "App", "to": "VNPay", "label": "y" } ] }
                ] }
              ]
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must have exactly one branch"));
    }

    [Fact]
    public void ValidateDiagram_ValidBusinessContext_ShouldReturnValidWithoutWarnings()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.BusinessContext,
            ModelJson = """
            {
              "title": "Order Context",
              "system": "Order Service",
              "internalComponents": ["Order API"],
              "externalSystems": ["Customer"],
              "relations": [
                { "from": "Customer", "to": "Order API", "protocol": "Place order" },
                { "from": "Order Service", "to": "Customer", "protocol": "Order status" }
              ]
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDiagram_BusinessContextWithoutExternalEntity_ShouldReturnInvalid()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.BusinessContext,
            ModelJson = """
            {
              "title": "Order Context",
              "system": "Order Service",
              "internalComponents": ["Order API"],
              "externalSystems": [],
              "relations": []
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least one external entity"));
    }

    [Fact]
    public void ValidateDiagram_BusinessContextFlowBetweenExternals_ShouldWarn()
    {
        // Arrange
        var request = new DiagramValidationRequest
        {
            DiagramType = (int)DiagramType.BusinessContext,
            ModelJson = """
            {
              "title": "Order Context",
              "system": "Order Service",
              "externalSystems": ["Customer", "Bank"],
              "relations": [
                { "from": "Customer", "to": "Order Service", "protocol": "Place order" },
                { "from": "Customer", "to": "Bank", "protocol": "Card details" }
              ]
            }
            """
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("between two external entities"));
        result.Warnings.Should().Contain(w => w.Contains("'Bank' has no data flow"));
    }

    private static DiagramValidationRequest UseCaseRequest(string modelJson) => new()
    {
        DiagramType = (int)DiagramType.UseCase,
        ModelJson = modelJson
    };

    [Fact]
    public void ValidateDiagram_UseCaseWithAllRelationTypes_ShouldReturnValidWithoutWarnings()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student", "Canteen Staff", "Canteen Manager"],
              "useCases": ["Place order", "Log in", "Apply voucher", "Confirm order"],
              "relations": [
                { "from": "Student", "to": "Place order" },
                { "from": "Canteen Staff", "to": "Confirm order", "type": "association" },
                { "from": "Place order", "to": "Log in", "type": "include" },
                { "from": "Apply voucher", "to": "Place order", "type": "extend" },
                { "from": "Canteen Manager", "to": "Canteen Staff", "type": "generalization" }
              ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDiagram_UseCaseIncludeFromActor_ShouldReturnInvalid()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student"],
              "useCases": ["Log in"],
              "relations": [ { "from": "Student", "to": "Log in", "type": "include" } ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must connect two use cases"));
    }

    [Fact]
    public void ValidateDiagram_UseCaseUnsupportedRelationType_ShouldReturnInvalid()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student"],
              "useCases": ["Place order"],
              "relations": [ { "from": "Student", "to": "Place order", "type": "uses" } ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("type 'uses' is not supported"));
    }

    [Fact]
    public void ValidateDiagram_UseCaseGeneralizationBetweenActorAndUseCase_ShouldReturnInvalid()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student"],
              "useCases": ["Place order"],
              "relations": [ { "from": "Student", "to": "Place order", "type": "generalization" } ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must connect two actors or two use cases"));
    }

    [Fact]
    public void ValidateDiagram_UseCaseAssociationBetweenTwoUseCases_ShouldReturnInvalid()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student"],
              "useCases": ["Place order", "Log in"],
              "relations": [
                { "from": "Student", "to": "Place order" },
                { "from": "Place order", "to": "Log in" }
              ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must connect an actor with a use case"));
    }

    [Fact]
    public void ValidateDiagram_UseCaseOrphanUseCaseAndUnconnectedActor_ShouldWarn()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student", "Auditor"],
              "useCases": ["Place order", "Export report"],
              "relations": [ { "from": "Student", "to": "Place order" } ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Use case 'Export report' is not used by any actor"));
        result.Warnings.Should().Contain(w => w.Contains("Actor 'Auditor' is not connected to any use case"));
    }

    [Fact]
    public void ValidateDiagram_UseCaseReachableViaInclude_ShouldNotWarnAsOrphan()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student"],
              "useCases": ["Place order", "Log in"],
              "relations": [
                { "from": "Student", "to": "Place order" },
                { "from": "Place order", "to": "Log in", "type": "include" }
              ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDiagram_UseCaseIncludeCycle_ShouldReturnInvalid()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student"],
              "useCases": ["Place order", "Pay online"],
              "relations": [
                { "from": "Student", "to": "Place order" },
                { "from": "Place order", "to": "Pay online", "type": "include" },
                { "from": "Pay online", "to": "Place order", "type": "include" }
              ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'include' relations must not form a cycle"));
    }

    [Fact]
    public void ValidateDiagram_UseCaseNameUsedByActorAndUseCase_ShouldReturnInvalid()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student", "Payment"],
              "useCases": ["Payment"],
              "relations": [ { "from": "Student", "to": "Payment" } ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'Payment' is declared as both an actor and a use case"));
    }

    [Fact]
    public void ValidateDiagram_UseCaseDuplicateRelation_ShouldWarn()
    {
        // Arrange
        var request = UseCaseRequest("""
            {
              "title": "Canteen",
              "actors": ["Student"],
              "useCases": ["Place order"],
              "relations": [
                { "from": "Student", "to": "Place order" },
                { "from": "Student", "to": "Place order", "type": "association" }
              ]
            }
            """);

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("is declared more than once"));
    }
}
