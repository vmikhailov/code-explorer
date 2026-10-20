import { PubSubConfig } from './pubsub.config';

export class GooglePubSubService {
  subscribeToMessages(subName: string, handler: (msg: any) => void): void {}
}

export class BundlesCacheService {
  constructor(
    private readonly pubSubService: GooglePubSubService,
    private readonly pubsubConfig: PubSubConfig,
  ) {
    this.pubSubService.subscribeToMessages(
      this.pubsubConfig.eventBusSubscriptionName,
      this.handleMessage.bind(this),
    );
  }

  handleMessage(msg: any): void {}
}
