export class PubSubConfig {
  readonly eventBusTopicName: string = process.env.EVENT_BUS_TOPIC_NAME || 'event-bus-topic';
  readonly eventBusSubscriptionName: string = process.env.EVENT_BUS_SUBSCRIPTION_NAME || 'event-bus-sub';
  readonly journalTopicName: string = process.env.JOURNAL_TOPIC_NAME || 'journal-topic';
  readonly journalSubscriptionName: string = process.env.JOURNAL_SUBSCRIPTION_NAME || 'journal-sub';
}
