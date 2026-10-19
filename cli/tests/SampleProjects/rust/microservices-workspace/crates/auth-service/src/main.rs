use domain_models::User;

pub async fn list_users() {
    let sql = "SELECT * FROM users";
    println!("{}", sql);
}

fn main() {
    println!("Auth service");
}
