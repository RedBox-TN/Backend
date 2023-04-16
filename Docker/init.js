db = db.getSiblingDB('redbox')
db.createCollection('Chats')
db.createCollection('Users')
db.createCollection('Roles')
db.createCollection('Powers')

db.Users.createIndex(
    {
        "email": 1
    },
    {
        unique: true
    }
)
db.Users.createIndex(
    {
        "username": 1
    },
    {
        unique: true
    }
)

db.Roles.createIndex(
    {
        "name": 1
    },
    {
        unique: true
    }
)


db = db.getSiblingDB('keychain')
db.createCollection('UsersPublicKeys')
db.createCollection('UsersPrivateKeys')
db.createCollection('GroupsPublicKeys')
db.createCollection('GroupsPrivateKey')
db.createCollection('ExecutivesPublicKeys')
db.createCollection('ExecutivesPrivateKeys')