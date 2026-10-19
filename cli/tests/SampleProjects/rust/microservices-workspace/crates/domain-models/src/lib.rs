pub struct User {
    pub id: u64,
    pub name: String,
}

pub trait UserRepository {
    fn get_user(&self, id: u64) -> Option<User>;
}

pub struct InMemoryUserRepo;

impl UserRepository for InMemoryUserRepo {
    fn get_user(&self, id: u64) -> Option<User> {
        Some(User {
            id,
            name: "Alice".to_string(),
        })
    }
}
