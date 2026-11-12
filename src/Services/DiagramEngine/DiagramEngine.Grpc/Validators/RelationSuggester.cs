using UMLForge.Contracts.Grpc;
using UMLForge.DiagramEngine.Grpc.Models;

namespace UMLForge.DiagramEngine.Grpc.Validators;

/// <summary>Gợi ý quan hệ còn thiếu (actor chưa nối, participant không nhận message).</summary>
public interface IRelationSuggester
{
    RelationSuggestionResponse Suggest(RelationSuggestionRequest request);
}

/// <summary>Chép nguyên logic <c>SuggestRelations</c> của v1.</summary>
public class RelationSuggester : IRelationSuggester
{
    public RelationSuggestionResponse Suggest(RelationSuggestionRequest request)
    {
        var response = new RelationSuggestionResponse();
        var diagramType = (DiagramType)request.DiagramType;

        if (!DiagramModelParser.TryParse(diagramType, request.ModelJson, out var model, out _))
        {
            return response;
        }

        if (model is UseCaseModel uc && uc.Actors.Count > 0 && uc.UseCases.Count > 0)
        {
            var connectedActors = uc.Relations
                .Where(r => r.RelationType is "association" or "generalization")
                .SelectMany(r => new[] { r.From, r.To })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var actor in uc.Actors)
            {
                if (!connectedActors.Contains(actor))
                {
                    response.Suggestions.Add(new RelationSuggestion
                    {
                        From = actor,
                        To = uc.UseCases[0],
                        Description = $"Actor '{actor}' is currently unconnected. Suggested relation to primary use case '{uc.UseCases[0]}'."
                    });
                }
            }
        }
        else if (model is SequenceModel seq && seq.Participants.Count >= 2)
        {
            var receivers = seq.GetAllMessages().Select(m => m.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var participant in seq.Participants)
            {
                if (!receivers.Contains(participant) && participant != seq.Participants[0])
                {
                    response.Suggestions.Add(new RelationSuggestion
                    {
                        From = seq.Participants[0],
                        To = participant,
                        Description = $"Participant '{participant}' receives no messages. Consider adding an inquiry message from '{seq.Participants[0]}'."
                    });
                }
            }
        }

        return response;
    }
}
