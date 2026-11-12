using Microsoft.Extensions.DependencyInjection;
using UMLForge.BuildingBlocks.EventBus;
using UMLForge.Contracts.Events;
using UMLForge.Notification.Repositories;
using UMLForge.Notification.Repositories.Repositories;
using UMLForge.Notification.Service.EventHandlers;
using UMLForge.Notification.Service.Validators;
using UMLForge.Notification.Service.Services;
namespace UMLForge.Notification.Service;
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationConsumers(this IServiceCollection services)
    {
        services.AddScoped<INotificationEventRepository, NotificationEventRepository>();
        services.AddSingleton<NotificationEventValidator>();
        services.AddScoped<NotificationMaintenanceService>();
        services.AddEventConsumer<NotificationDbContext>(StreamNames.Workspace, ConsumerGroups.NotificationSvc)
            .On<ProjectCreated, NotificationEventHandler>().On<InvitationSent, NotificationEventHandler>()
            .On<MemberAdded, NotificationEventHandler>().On<MemberRemoved, NotificationEventHandler>()
            .On<LecturerAssigned, NotificationEventHandler>().On<ProjectDeleted, NotificationEventHandler>();
        services.AddEventConsumer<NotificationDbContext>(StreamNames.Diagram, ConsumerGroups.NotificationSvc)
            .On<ReviewSubmitted, NotificationEventHandler>().On<ReviewDecided, NotificationEventHandler>().On<CommentAdded, NotificationEventHandler>();
        services.AddEventConsumer<NotificationDbContext>(StreamNames.Render, ConsumerGroups.NotificationSvc)
            .On<RenderCompleted, NotificationEventHandler>().On<RenderFailed, NotificationEventHandler>();
        return services;
    }
}
