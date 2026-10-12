from django.urls import path

def order_list(request):
    pass

def user_profile(request, id):
    pass

urlpatterns = [
    path('api/v1/orders/', order_list),
    path('users/<int:id>/', user_profile),
]
