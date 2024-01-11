db.createUser(
    {
        user: "admin",
        pwd: "",
        roles: [{ role: "root", db: "admin" }],
        authenticationRestrictions: [{ clientSource: ["127.0.0.1"] }]
    }
)

rs.initiate(
    {
        _id: "redbox",
        members: [
            { _id: 0, host: "", "priority": 1 },
            { _id: 1, host: "", "priority": 0.5 },
            { _id: 2, host: "", "priority": 0.5 }
        ]
    }
)

db = db.getSiblingDB('redbox-accounts')
db.createCollection('Users')
db.createCollection('Roles')

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

db = db.getSiblingDB('redbox-keychain')
db.createCollection('UsersMasterKeys')
db.createCollection('UsersPublicKeys')
db.createCollection('UsersPrivateKeys')
db.createCollection('ChatsKeys')
db.createCollection('GroupsKeys')
db.createCollection('SupervisorsMasterKeys')
db.createCollection('SupervisedChatsKeys')
db.createCollection('SupervisedGroupsKeys')
db.createCollection("SupervisorPrivateKey", {
    capped: true,
    size: 4096,
    max: 1
})
db.createCollection("SupervisorPublicKey", {
    capped: true,
    size: 4096,
    max: 1
})

db.UsersMasterKeys.createIndex(
    {
        "UserOwnerId": 1
    },
    {
        unique: true
    }
)

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

db.ChatsKeys.createIndex(
    {
        "UserOwnerId": 1,
        "IsEncryptedWithUserPublicKey": 1
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

db.GroupsKeys.createIndex(
    {
        "UserOwnerId": 1,
        "IsEncryptedWithUserPublicKey": 1
    },
    {
        unique: true
    }
)

db.SupervisorsMasterKeys.createIndex(
    {
        "UserOwnerId": 1
    },
    {
        unique: true
    }
)

db.SupervisorsMasterKeys.createIndex(
    {
        "UserOwnerId": 1,
        "IsEncryptedWithUserPublicKey": 1
    },
    {
        unique: true
    }
)

db.SupervisedChatsKeys.createIndex(
    {
        "ChatCollectionName": 1
    },
    {
        unique: true
    }
)

db.SupervisedChatsKeys.createIndex(
    {
        "ChatCollectionName": 1,
        "IsEncryptedWithUserPublicKey": 1
    },
    {
        unique: true
    }
)

db.SupervisedGroupsKeys.createIndex(
    {
        "ChatCollectionName": 1
    },
    {
        unique: true
    }
)

db.SupervisedGroupsKeys.createIndex(
    {
        "ChatCollectionName": 1,
        "IsEncryptedWithUserPublicKey": 1
    },
    {
        unique: true
    }
)

db = db.getSiblingDB('redbox')
db.createCollection('ChatDetails')
db.createCollection('GroupDetails')

db.ChatDetails.createIndex(
    {
        "MembersIds": 1
    }
)

db.GroupDetails.createIndex(
    {
        "AdminsIds": 1
    }
)

db.GroupDetails.createIndex(
    {
        "MembersIds": 1
    }
)

db.GroupDetails.createIndex(
    {
        "Name": 1
    }
)

db = db.getSiblingDB('redbox-chats')
db.createCollection('Default')

db = db.getSiblingDB('redbox-groups')
db.createCollection('Broadcast')

db = db.getSiblingDB('admin')
db.createUser(
    {
        user: "redboxKeychain",
        pwd: "",
        roles: [
            { role: "readWrite", db: "redbox-keychain" }
        ]
    }
)

db.createUser(
    {
        user: "redboxUsers",
        pwd: "",
        roles: [
            { role: "readWrite", db: "redbox-accounts" }
        ]
    }
)

db.createUser(
    {
        user: "redbox",
        pwd: "",
        roles: [
            { role: "readWrite", db: "redbox" },
            { role: "readWrite", db: "redbox-chats" },
            { role: "readWrite", db: "redbox-groups" }
        ]
    }
)
