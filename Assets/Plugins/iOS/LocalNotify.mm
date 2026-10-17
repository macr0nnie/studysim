// Local notification for the study timer; called from Assets/Scripts/LocalNotify.cs.
#import <UserNotifications/UserNotifications.h>

static NSString* const LofiTimerId = @"lofilife.timer";

extern "C" {

void LofiNotifyAsk()
{
    [[UNUserNotificationCenter currentNotificationCenter]
        requestAuthorizationWithOptions:(UNAuthorizationOptionAlert | UNAuthorizationOptionSound)
        completionHandler:^(BOOL granted, NSError* error) {}];
}

void LofiNotifySchedule(const char* title, const char* body, double seconds)
{
    if (seconds < 1) return;
    UNMutableNotificationContent* content = [[UNMutableNotificationContent alloc] init];
    content.title = [NSString stringWithUTF8String:title];
    content.body = [NSString stringWithUTF8String:body];
    content.sound = [UNNotificationSound defaultSound];
    UNTimeIntervalNotificationTrigger* trigger = [UNTimeIntervalNotificationTrigger triggerWithTimeInterval:seconds repeats:NO];
    UNNotificationRequest* request = [UNNotificationRequest requestWithIdentifier:LofiTimerId content:content trigger:trigger];
    [[UNUserNotificationCenter currentNotificationCenter] addNotificationRequest:request withCompletionHandler:nil];
}

void LofiNotifyCancel()
{
    [[UNUserNotificationCenter currentNotificationCenter] removePendingNotificationRequestsWithIdentifiers:@[LofiTimerId]];
}

}
