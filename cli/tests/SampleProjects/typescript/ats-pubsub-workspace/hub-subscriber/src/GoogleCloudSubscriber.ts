import { Message, PubSub, Subscription } from '@google-cloud/pubsub';

let subscription: Subscription | null = null;

const generateUniqueTopicSubName = (topicNameOrId: string): string => {
  return `${topicNameOrId}-sub-unique`;
};

export const subscriberKeepAlive = async (): Promise<void> => {
  const topicNameOrId = process.env.EVENT_TB_UPDATED_TOPIC_NAME || 'event-tb-updated-topic';
  const subscriptionName = generateUniqueTopicSubName(topicNameOrId);

  const pubsub = new PubSub();
  const topic = await pubsub.topic(topicNameOrId);

  [subscription] = await topic.createSubscription(subscriptionName);

  subscription.on('message', async (message: Message) => {
    await message.ackWithResponse();
  });
};
