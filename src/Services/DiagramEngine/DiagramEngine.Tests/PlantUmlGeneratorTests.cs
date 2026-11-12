using FluentAssertions;
using UMLForge.DiagramEngine.Grpc.Models;
using UMLForge.DiagramEngine.Grpc.Generators;
using Xunit;

namespace UMLForge.DiagramEngine.Tests;

public class PlantUmlGeneratorTests
{
    private readonly PlantUmlGenerator _generator = new();

    private static string[] Lines(string plantUml) => plantUml.Split('\n');

    public static TheoryData<DiagramType, string> ContractSamples => new()
    {
        {
            DiagramType.BusinessContext,
            """
            {
              "type": "BusinessContext",
              "title": "E-Commerce System Context",
              "system": "Order Fulfillment Service",
              "internalComponents": ["Order API", "Inventory Worker", "Payment Gateway Proxy"],
              "externalSystems": ["Customer App", "Payment Gateway", "Logistics Partner"],
              "relations": [
                { "from": "Customer App", "to": "Order API", "protocol": "HTTPS" },
                { "from": "Order API", "to": "Payment Gateway Proxy", "protocol": "gRPC" }
              ]
            }
            """
        },
        {
            DiagramType.UseCase,
            """
            {
              "type": "UseCase",
              "title": "Hospital Management - Appointment Booking",
              "systemBoundary": "Hospital Portal",
              "actors": ["Patient", "Doctor"],
              "useCases": ["Book Appointment", "View Schedule"],
              "relations": [
                { "from": "Patient", "to": "Book Appointment" },
                { "from": "Doctor", "to": "View Schedule" }
              ]
            }
            """
        },
        {
            DiagramType.Sequence,
            """
            {
              "type": "Sequence",
              "title": "Course Registration Flow",
              "participants": ["Student", "Registration API"],
              "messages": [
                { "from": "Student", "to": "Registration API", "label": "register(courseId)", "isResponse": false },
                { "from": "Registration API", "to": "Student", "label": "success(202)", "isResponse": true }
              ]
            }
            """
        },
        {
            DiagramType.Activity,
            """
            {
              "type": "Activity",
              "title": "Student Registration Workflow",
              "startNode": "Open Registration Portal",
              "steps": ["Enter Course Code", "Validate Prerequisites", "Enroll Student"],
              "decisions": [
                { "condition": "Prerequisites met?", "yesBranch": "Enroll Student", "noBranch": "Display Error" }
              ]
            }
            """
        },
        {
            DiagramType.State,
            """
            {
              "type": "State",
              "title": "Render Job Lifecycle",
              "initialState": "Draft",
              "states": ["Draft", "Pending", "Rendering", "Rendered", "Failed"],
              "transitions": [
                { "from": "Draft", "event": "submit", "to": "Pending" },
                { "from": "Pending", "event": "worker_dequeue", "to": "Rendering" },
                { "from": "Rendering", "event": "render_success", "to": "Rendered" }
              ]
            }
            """
        }
    };

    [Theory]
    [MemberData(nameof(ContractSamples))]
    public void GeneratePlantUml_ContractSample_ShouldWrapInStartEndTagsWithTitle(DiagramType type, string modelJson)
    {
        // Arrange
        var model = DiagramModelParser.Parse(type, modelJson);

        // Act
        var result = _generator.GeneratePlantUml(type, model);

        // Assert
        result.Should().StartWith("@startuml\n");
        result.Should().EndWith("\n@enduml");
        result.Should().Contain($"title {model.Title}");
    }

    [Fact]
    public void GeneratePlantUml_UseCase_ShouldPlaceUseCasesInsideBoundaryAndLinkActors()
    {
        // Arrange
        var model = new UseCaseModel
        {
            Title = "Hospital",
            SystemBoundary = "Hospital Portal",
            Actors = ["Patient"],
            UseCases = ["Book Appointment"],
            Relations = [new UseCaseRelation { From = "Patient", To = "Book Appointment" }]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.UseCase, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            "actor \"Patient\" as A0",
            "rectangle \"Hospital Portal\" {",
            "  usecase \"Book Appointment\" as UC0",
            "}",
            "A0 -- UC0");
    }

    [Fact]
    public void GeneratePlantUml_UseCaseRelationToUndeclaredElement_ShouldBeSkipped()
    {
        // Arrange
        var model = new UseCaseModel
        {
            Title = "Hospital",
            Actors = ["Patient"],
            UseCases = ["Book Appointment"],
            Relations = [new UseCaseRelation { From = "Patient", To = "Pay Invoice" }]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.UseCase, model);

        // Assert
        result.Should().NotContain("-->");
        result.Should().NotContain(" -- ");
        result.Should().NotContain("Pay Invoice");
    }

    [Fact]
    public void GeneratePlantUml_UseCaseRelationTypes_ShouldUseUmlNotation()
    {
        // Arrange
        var model = new UseCaseModel
        {
            Title = "Canteen",
            SystemBoundary = "CampusEats",
            Actors = ["Student", "Canteen Staff", "Canteen Manager"],
            UseCases = ["Place order", "Log in", "Apply voucher"],
            Relations =
            [
                new UseCaseRelation { From = "Student", To = "Place order" },
                new UseCaseRelation { From = "Place order", To = "Log in", Type = "include" },
                new UseCaseRelation { From = "Apply voucher", To = "Place order", Type = "extend" },
                new UseCaseRelation { From = "Canteen Manager", To = "Canteen Staff", Type = "generalization" }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.UseCase, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            "A0 -- UC0",
            "UC0 ..> UC1 : <<include>>",
            "UC2 ..> UC0 : <<extend>>",
            "A1 <|-- A2");
        result.Should().NotContain("-->");
    }

    [Fact]
    public void GeneratePlantUml_UseCaseUnsupportedRelationType_ShouldBeSkipped()
    {
        // Arrange
        var model = new UseCaseModel
        {
            Title = "Canteen",
            Actors = ["Student"],
            UseCases = ["Place order"],
            Relations = [new UseCaseRelation { From = "Student", To = "Place order", Type = "uses" }]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.UseCase, model);

        // Assert
        result.Should().Contain("actor \"Student\" as A0");
        Lines(result).Should().NotContain(line => line.StartsWith("A0 "));
    }

    [Fact]
    public void GeneratePlantUml_BusinessContext_ShouldDrawSystemInCenterWithExternalColumns()
    {
        // Arrange
        var model = new BusinessContextModel
        {
            Title = "Order Context",
            System = "Order Service",
            InternalComponents = ["Order API"],
            ExternalSystems = ["Customer", "Payment Gateway"],
            Relations =
            [
                new ContextRelation { From = "Customer", To = "Order API", Protocol = "Place order" },
                new ContextRelation { From = "Order Service", To = "Customer", Protocol = "Order status" },
                new ContextRelation { From = "Order API", To = "Payment Gateway", Protocol = "Charge request" },
                new ContextRelation { From = "Payment Gateway", To = "Order Service", Protocol = "Payment result" }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.BusinessContext, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            "rectangle \"Customer\" as E0",
            "usecase \"\\n\\n\\n\\nOrder Service\\n\\n\\n\\n\" as SYS",
            "rectangle \"Payment Gateway\" as E1",
            "label \"Place order\" as F0",
            "E0 -- F0",
            "F0 --> SYS",
            "label \"Order status\" as F1",
            "E0 <-- F1",
            "F1 -- SYS",
            "label \"Charge request\" as F2",
            "SYS -- F2",
            "F2 --> E1",
            "label \"Payment result\" as F3",
            "SYS <-- F3",
            "F3 -- E1");
        result.Should().NotContain("Order API");
    }

    [Fact]
    public void GeneratePlantUml_BusinessContextInternalOrExternalOnlyFlows_ShouldBeSkipped()
    {
        // Arrange
        var model = new BusinessContextModel
        {
            Title = "Order Context",
            System = "Order Service",
            InternalComponents = ["Order API", "Billing Worker"],
            ExternalSystems = ["Customer", "Payment Gateway"],
            Relations =
            [
                new ContextRelation { From = "Order API", To = "Billing Worker", Protocol = "Invoice job" },
                new ContextRelation { From = "Customer", To = "Payment Gateway", Protocol = "Card details" }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.BusinessContext, model);

        // Assert
        result.Should().NotContain("-->");
        result.Should().NotContain("<--");
        result.Should().NotContain("label ");
    }

    [Fact]
    public void GeneratePlantUml_BusinessContextExternals_ShouldAlternateLeftAndRightColumns()
    {
        // Arrange
        var externals = new List<string> { "A", "B", "C" };
        var model = new BusinessContextModel
        {
            Title = "Context",
            System = "Core",
            ExternalSystems = externals,
            Relations = externals.Select(name => new ContextRelation { From = "Core", To = name, Protocol = "data" }).ToList()
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.BusinessContext, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            "rectangle \"A\" as E0",
            "rectangle \"C\" as E2",
            "usecase \"\\n\\n\\n\\nCore\\n\\n\\n\\n\" as SYS",
            "rectangle \"B\" as E1",
            "E0 <-- F0",
            "F0 -- SYS",
            "SYS -- F1",
            "F1 --> E1",
            "E2 <-- F2",
            "F2 -- SYS");
    }

    [Fact]
    public void GeneratePlantUml_BusinessContextFlowWithoutLabel_ShouldDrawDirectArrow()
    {
        // Arrange
        var model = new BusinessContextModel
        {
            Title = "Context",
            System = "Core",
            ExternalSystems = ["Customer", "Bank"],
            Relations =
            [
                new ContextRelation { From = "Customer", To = "Core" },
                new ContextRelation { From = "Bank", To = "Core" }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.BusinessContext, model);

        // Assert
        Lines(result).Should().ContainInOrder("E0 --> SYS", "SYS <-- E1");
        result.Should().NotContain("label ");
    }

    [Fact]
    public void GeneratePlantUml_BusinessContextLongSystemName_ShouldWrapIntoShortLines()
    {
        // Arrange
        var model = new BusinessContextModel
        {
            Title = "Context",
            System = "LocalMate AI Trip Planner Platform",
            ExternalSystems = ["Traveler"],
            Relations = [new ContextRelation { From = "Traveler", To = "LocalMate AI Trip Planner Platform", Protocol = "Trip request" }]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.BusinessContext, model);

        // Assert
        result.Should().Contain("usecase \"\\n\\n\\n\\nLocalMate AI\\nTrip Planner\\nPlatform\\n\\n\\n\\n\" as SYS");
        result.Should().Contain("label \"Trip request\" as F0");
    }

    [Fact]
    public void GeneratePlantUml_Sequence_ShouldUseSolidArrowForRequestAndDashedArrowForResponse()
    {
        // Arrange
        var model = new SequenceModel
        {
            Title = "Register",
            Participants = ["Student", "Registration API"],
            Messages =
            [
                new SequenceMessage { From = "Student", To = "Registration API", Label = "register(courseId)" },
                new SequenceMessage { From = "Registration API", To = "Student", Label = "success(202)", IsResponse = true }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.Sequence, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            "participant \"Student\" as P0",
            "participant \"Registration API\" as P1",
            "P0 -> P1 : register(courseId)",
            "P1 --> P0 : success(202)");
    }

    [Fact]
    public void GeneratePlantUml_ActivityDecisionMatchingStep_ShouldBePlacedAtThatStep()
    {
        // Arrange
        var model = new ActivityModel
        {
            Title = "Registration",
            StartNode = "Open Registration Portal",
            Steps = ["Enter Course Code", "Validate Prerequisites", "Enroll Student"],
            Decisions =
            [
                new ActivityDecision { Condition = "Prerequisites met?", YesBranch = "Enroll Student", NoBranch = "Display Error" }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.Activity, model);

        // Assert
        var lines = Lines(result);
        lines.Should().ContainInOrder(
            "start",
            ":Open Registration Portal;",
            ":Enter Course Code;",
            ":Validate Prerequisites;",
            "if (Prerequisites met?) then (yes)",
            "  :Enroll Student;",
            "else (no)",
            "  :Display Error;",
            "endif",
            "stop");
        lines.Count(line => line.Trim() == ":Enroll Student;").Should().Be(1);
    }

    [Fact]
    public void GeneratePlantUml_ActivityDecisionNotMatchingAnyStep_ShouldBeAppendedAfterLastStep()
    {
        // Arrange
        var model = new ActivityModel
        {
            Title = "Checkout",
            Steps = ["Add To Cart", "Enter Address"],
            Decisions =
            [
                new ActivityDecision { Condition = "Paid?", YesBranch = "Ship Order", NoBranch = "Cancel Order" }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.Activity, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            ":Add To Cart;",
            ":Enter Address;",
            "if (Paid?) then (yes)",
            "  :Ship Order;",
            "else (no)",
            "  :Cancel Order;",
            "endif",
            "stop");
    }

    [Fact]
    public void GeneratePlantUml_State_ShouldMarkStatesWithoutOutgoingTransitionAsFinal()
    {
        // Arrange
        var model = new StateModel
        {
            Title = "Job",
            InitialState = "Draft",
            States = ["Draft", "Pending", "Rendered", "Failed"],
            Transitions =
            [
                new StateTransition { From = "Draft", Event = "submit", To = "Pending" },
                new StateTransition { From = "Pending", Event = "render_success", To = "Rendered" },
                new StateTransition { From = "Pending", Event = "render_error", To = "Failed" }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.State, model);

        // Assert
        var lines = Lines(result);
        lines.Should().ContainInOrder(
            "state \"Draft\" as S0",
            "[*] --> S0",
            "S0 --> S1 : submit",
            "S1 --> S2 : render_success",
            "S1 --> S3 : render_error");
        lines.Should().Contain("S2 --> [*]").And.Contain("S3 --> [*]");
        lines.Should().NotContain("S0 --> [*]").And.NotContain("S1 --> [*]");
    }

    [Fact]
    public void GeneratePlantUml_NameContainingDoubleQuote_ShouldBeEscaped()
    {
        // Arrange
        var model = new UseCaseModel
        {
            Title = "Quotes",
            Actors = ["The \"Admin\""],
            UseCases = ["Manage"]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.UseCase, model);

        // Assert
        result.Should().Contain("actor \"The 'Admin'\" as A0");
    }

    [Fact]
    public void GeneratePlantUml_ModelNotMatchingType_ShouldThrowArgumentException()
    {
        // Arrange
        var model = new SequenceModel { Title = "Wrong" };

        // Act
        var act = () => _generator.GeneratePlantUml(DiagramType.UseCase, model);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*does not match*");
    }

    [Fact]
    public void GeneratePlantUml_UndefinedType_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var model = new UseCaseModel { Title = "Invalid" };

        // Act
        var act = () => _generator.GeneratePlantUml((DiagramType)99, model);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GeneratePlantUml_NullModel_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _generator.GeneratePlantUml(DiagramType.UseCase, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GeneratePlantUml_SequenceAltFragment_ShouldWriteAltElseEndBlock()
    {
        // Arrange
        var model = new SequenceModel
        {
            Title = "Pay",
            Participants = ["App", "VNPay"],
            Messages =
            [
                new SequenceMessage { From = "App", To = "VNPay", Label = "pay" },
                new SequenceMessage
                {
                    Fragment = "alt",
                    Branches =
                    [
                        new SequenceBranch
                        {
                            Condition = "success",
                            Messages = [new SequenceMessage { From = "VNPay", To = "App", Label = "paid", IsResponse = true }]
                        },
                        new SequenceBranch
                        {
                            Condition = "failed",
                            Messages = [new SequenceMessage { From = "VNPay", To = "App", Label = "error", IsResponse = true }]
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.Sequence, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            "P0 -> P1 : pay",
            "alt success",
            "  P1 --> P0 : paid",
            "else failed",
            "  P1 --> P0 : error",
            "end");
    }

    [Fact]
    public void GeneratePlantUml_SequenceLoopNestedInOpt_ShouldIndentAndCloseEachBlock()
    {
        // Arrange
        var model = new SequenceModel
        {
            Title = "Cart",
            Participants = ["API", "DB"],
            Messages =
            [
                new SequenceMessage
                {
                    Fragment = "opt",
                    Branches =
                    [
                        new SequenceBranch
                        {
                            Condition = "cart not empty",
                            Messages =
                            [
                                new SequenceMessage
                                {
                                    Fragment = "loop",
                                    Branches =
                                    [
                                        new SequenceBranch
                                        {
                                            Condition = "each dish",
                                            Messages = [new SequenceMessage { From = "API", To = "DB", Label = "check stock" }]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _generator.GeneratePlantUml(DiagramType.Sequence, model);

        // Assert
        Lines(result).Should().ContainInOrder(
            "opt cart not empty",
            "  loop each dish",
            "    P0 -> P1 : check stock",
            "  end",
            "end");
    }

    [Fact]
    public void GeneratePlantUml_SequenceUnsupportedFragment_ShouldBeSkipped()
    {
        // Arrange
        var model = new SequenceModel
        {
            Title = "Unsupported",
            Participants = ["A", "B"],
            Messages =
            [
                new SequenceMessage
                {
                    Fragment = "par",
                    Branches = [new SequenceBranch { Condition = "x", Messages = [new SequenceMessage { From = "A", To = "B", Label = "hi" }] }]
                }
            ]
        };

        // Act
        var lines = Lines(_generator.GeneratePlantUml(DiagramType.Sequence, model));

        // Assert
        lines.Should().NotContain(line => line.StartsWith("par "));
        lines.Should().NotContain("end");
        lines.Should().NotContain("P0 -> P1 : hi");
    }
}
