from flask import Flask, Blueprint

app = Flask(__name__)
bp = Blueprint('api_v1', __name__, url_prefix='/api/v1')

@app.route('/health')
def health_check():
    return {'status': 'healthy'}

@bp.route('/items', methods=['POST'])
def create_item():
    return {'id': 1}

@app.get('/users/<int:user_id>')
def get_user(user_id):
    return {'id': user_id}

app.register_blueprint(bp)

if __name__ == '__main__':
    app.run(port=5000)
