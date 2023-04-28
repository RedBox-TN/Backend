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
        "Username": 1
    },
    {
        unique: true
    }
)

db.Roles.insertOne(
    {
        "_id": ObjectId("642579fd406480a0d6208945"),
        "Name": "admin",
        "Permissions": 129
    }
)

db = db.getSiblingDB('redbox-keychain')
db.createCollection('UsersPublicKeys')
db.createCollection('UsersPrivateKeys')
db.createCollection('GroupsPublicKeys')
db.createCollection('GroupsPrivateKeys')
db.createCollection('SupervisorsPublicKeys')
db.createCollection('SupervisorsPrivateKeys')

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

db.GroupsPublicKeys.createIndex(
    {
        "GroupCollectionName": 1
    },
    {
        unique: true
    }
)

db.GroupsPrivateKeys.createIndex(
    {
        "UserOwnerId": 1,
        "GroupCollectionName": 1
    },
    {
        unique: true
    }
)

db.SupervisorsPrivateKeys.createIndex(
    {
        "UserOwnerId": 1
    },
    {
        unique: true
    }
)

db = db.getSiblingDB('redbox')
db.createCollection('Chats')
db.createCollection('Groups')

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

db = db.getSiblingDB('redbox-chats')
db.createCollection('Broadcast')

db = db.getSiblingDB('redbox-supervisedChats')
db.createCollection('Default')

db = db.getSiblingDB('redbox-groups')
db.createCollection('Default')

db = db.getSiblingDB('redbox-supervisedGroups')
db.createCollection('Default')