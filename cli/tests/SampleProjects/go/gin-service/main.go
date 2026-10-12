package main

import "github.com/gin-gonic/gin"

func getOrders(c *gin.Context) {
    c.JSON(200, gin.H{"status": "ok"})
}

func createOrder(c *gin.Context) {
    c.JSON(201, gin.H{"created": true})
}

func main() {
    r := gin.Default()
    api := r.Group("/api/v1")
    {
        api.GET("/orders", getOrders)
        api.POST("/orders", createOrder)
    }
    r.Run(":8080")
}
