package com.example;

import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.stereotype.Service;

@Service
public class RabbitOrderService {

    private final RabbitTemplate rabbitTemplate;

    public RabbitOrderService(RabbitTemplate rabbitTemplate) {
        this.rabbitTemplate = rabbitTemplate;
    }

    @RabbitListener(queues = "incoming-orders-queue")
    public void onOrderReceived(String orderData) {
        rabbitTemplate.convertAndSend("orders-exchange", "orders-completed", orderData);
    }
}
