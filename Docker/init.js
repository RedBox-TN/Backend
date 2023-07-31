db = db.getSiblingDB("admin")
db.createUser(
    {
        user: "redboxUsers",
        pwd: "pGv9n*@@c^wfFQRt$_W31D94v",
        roles: [
            { role: "readWrite", db: "redbox-accounts" }
        ]
    }
)

db.createUser(
    {
        user: "redboxKeychain",
        pwd: "9e51SfysAvkygFi9ohh|@AZ2J",
        roles: [
            { role: "readWrite", db: "redbox-keychain" }
        ]
    }
)

db.createUser(
    {
        user: "redbox",
        pwd: "M=EccO5rIKDtER**^t7Sw8*Vz",
        roles: [
            { role: "readWrite", db: "redbox" },
            { role: "readWrite", db: "redbox-chats" },
            { role: "readWrite", db: "redbox-groups" }
        ]
    }
)

db = db.getSiblingDB("redbox-accounts")
db.createCollection("Users")
db.createCollection("Roles")

db.Users.createIndex(
    {
        "Email": 1
    },
    {
        unique: true
    }
)
db.Users.createIndex(
    {
        "Username": 1
    },
    {
        unique: true
    }
)

db.Roles.createIndex(
    {
        "Name": 1
    },
    {
        unique: true
    }
)

db = db.getSiblingDB("redbox-keychain")
db.createCollection("UsersPublicKeys")
db.createCollection("UsersPrivateKeys")
db.createCollection("ChatsKeys")
db.createCollection("GroupsKeys")
db.createCollection("SupervisorsKeys")

db.UsersPublicKeys.createIndex(
    {
        "UserOwnerId": 1
    },
    {
        unique: true
    }
)

db.UsersPrivateKeys.createIndex(
    {
        "UserOwnerId": 1
    },
    {
        unique: true
    }
)

db.ChatsKeys.createIndex(
    {
        "UserOwnerId": 1,
        "ChatCollectionName": 1
    },
    {
        unique: true
    }
)

db.GroupsKeys.createIndex(
    {
        "UserOwnerId": 1,
        "ChatCollectionName": 1
    },
    {
        unique: true
    }
)

db.SupervisorsKeys.createIndex(
    {
        "UserOwnerId": 1,
        "ChatCollectionName": 1
    },
    {
        unique: true
    }
)

db = db.getSiblingDB("redbox")
db.createCollection("Chats")
db.createCollection("Groups")

db.Chats.createIndex(
    {
        "MembersIds": 1
    },
    {
        unique: true
    }
)

db.Chats.createIndex(
    {
        "CollectionName": 1
    },
    {
        unique: true
    }
)

db.Groups.createIndex(
    {
        "AdminsIds": 1
    },
    {
        unique: true
    }
)

db.Groups.createIndex(
    {
        "MembersIds": 1
    },
    {
        unique: true
    }
)

db.Groups.createIndex(
    {
        "CollectionName": 1
    },
    {
        unique: true
    }
)

db = db.getSiblingDB("redbox-chats")
db.createCollection("Default")

db = db.getSiblingDB("redbox-groups")
db.createCollection("Broadcast")