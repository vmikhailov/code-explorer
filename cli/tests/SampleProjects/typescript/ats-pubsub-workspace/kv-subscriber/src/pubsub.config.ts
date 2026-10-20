export class PubSubConfig {
  readonly topicName: string = process.env.EVENT_BUS_TOPIC_NAME || 'event-bus-topic';
  readonly subName: string = process.env.EVENT_SUBSCRIBER_NAME || 'event-bus-sub';
}
