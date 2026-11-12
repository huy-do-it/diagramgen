using FluentAssertions;
using UMLForge.DiagramEngine.Grpc.Models;
using Xunit;

namespace UMLForge.DiagramEngine.Tests;

public class DiagramModelParserTests
{
    [Fact]
    public void Parse_ValidUseCaseJson_ShouldReturnPopulatedModel()
    {
        // Arrange
        var json = """
        {
          "type": "UseCase",
          "title": "Course Registration",
          "systemBoundary": "LMS",
          "actors": ["Student", "Admin"],
          "useCases": ["Register Course", "Manage Users"],
          "relations": [
            { "from": "Student", "to": "Register Course" },
            { "from": "Admin", "to": "Manage Users" }
          ]
        }
        """;

        // Act
        var result = DiagramModelParser.Parse(DiagramType.UseCase, json);

        // Assert
        result.Should().BeOfType<UseCaseModel>();
        var uc = (UseCaseModel)result;
        uc.Title.Should().Be("Course Registration");
        uc.SystemBoundary.Should().Be("LMS");
        uc.Actors.Should().ContainInOrder("Student", "Admin");
        uc.UseCases.Should().ContainInOrder("Register Course", "Manage Users");
        uc.Relations.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ValidSequenceJson_ShouldReturnPopulatedModel()
    {
        // Arrange
        var json = """
        {
          "type": "Sequence",
          "title": "Login Flow",
          "participants": ["Browser", "API", "Database"],
          "messages": [
            { "from": "Browser", "to": "API", "label": "POST /login" },
            { "from": "API", "to": "Database", "label": "GetUser()" }
          ]
        }
        """;

        // Act
        var result = DiagramModelParser.Parse(DiagramType.Sequence, json);

        // Assert
        result.Should().BeOfType<SequenceModel>();
        var seq = (SequenceModel)result;
        seq.Title.Should().Be("Login Flow");
        seq.Participants.Should().HaveCount(3);
        seq.Messages.Should().HaveCount(2);
        seq.Messages[0].From.Should().Be("Browser");
        seq.Messages[0].To.Should().Be("API");
    }

    [Fact]
    public void Parse_ValidBusinessContextJson_ShouldReturnPopulatedModel()
    {
        // Arrange
        var json = """
        {
          "type": "BusinessContext",
          "title": "System Context",
          "system": "OrderService",
          "internalComponents": ["OrderAPI", "PaymentWorker"],
          "externalSystems": ["Bank", "Customer"],
          "relations": [
            { "from": "Customer", "to": "OrderAPI", "protocol": "HTTPS" }
          ]
        }
        """;

        // Act
        var result = DiagramModelParser.Parse(DiagramType.BusinessContext, json);

        // Assert
        result.Should().BeOfType<BusinessContextModel>();
        var bc = (BusinessContextModel)result;
        bc.System.Should().Be("OrderService");
        bc.InternalComponents.Should().Contain("OrderAPI");
        bc.ExternalSystems.Should().Contain("Bank");
    }

    [Fact]
    public void Parse_ValidActivityJson_ShouldReturnPopulatedModel()
    {
        // Arrange
        var json = """
        {
          "type": "Activity",
          "title": "Order Processing",
          "startNode": "Order Received",
          "steps": ["Verify Payment", "Pack Items", "Ship"],
          "decisions": [
            { "condition": "Payment OK?", "yesBranch": "Pack Items", "noBranch": "Cancel Order" }
          ]
        }
        """;

        // Act
        var result = DiagramModelParser.Parse(DiagramType.Activity, json);

        // Assert
        result.Should().BeOfType<ActivityModel>();
        var act = (ActivityModel)result;
        act.StartNode.Should().Be("Order Received");
        act.Steps.Should().HaveCount(3);
        act.Decisions.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_ValidStateJson_ShouldReturnPopulatedModel()
    {
        // Arrange
        var json = """
        {
          "type": "State",
          "title": "Order Lifecycle",
          "initialState": "Draft",
          "states": ["Draft", "Confirmed", "Shipped"],
          "transitions": [
            { "from": "Draft", "event": "checkout", "to": "Confirmed" }
          ]
        }
        """;

        // Act
        var result = DiagramModelParser.Parse(DiagramType.State, json);

        // Assert
        result.Should().BeOfType<StateModel>();
        var st = (StateModel)result;
        st.InitialState.Should().Be("Draft");
        st.States.Should().HaveCount(3);
        st.Transitions.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_MalformedJson_ShouldThrowFormatException()
    {
        // Arrange
        var malformedJson = "{ invalid json without quotes: 123 ";

        // Act
        var act = () => DiagramModelParser.Parse(DiagramType.UseCase, malformedJson);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Malformed diagram JSON*");
    }

    [Fact]
    public void TryParse_MalformedJson_ShouldReturnFalseWithErrorMessage()
    {
        // Arrange
        var malformedJson = "not a json string";

        // Act
        var success = DiagramModelParser.TryParse(DiagramType.UseCase, malformedJson, out var model, out var error);

        // Assert
        success.Should().BeFalse();
        model.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }
}
