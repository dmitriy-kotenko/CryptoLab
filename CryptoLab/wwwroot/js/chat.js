var connection = new signalR.HubConnectionBuilder().withUrl('/chatHub').build();

var serverPublicKey;
var selectedUser;

var handshakeCompletedSuccessfully;
var aesKey;

connection.on('ClientConnected', appendUser);

connection.on('UserList', function (users) {
    users.forEach(appendUser);
});

connection.on('ClientDisconnected', function (userEmail) {
    var userItem = document.getElementById(userEmail);
    document.getElementById('usersList').removeChild(userItem);
});

connection.on('ReceiveMessage', function (encryptedMessage) {
    var message = decryptMessage(encryptedMessage);
    appendMessage(selectedUser, message);
});

connection.on('StartHandshakeRequested', function (withUserEncryptedPublicKey, signature) {
    handshakeCompletedSuccessfully = false;
    console.log('with user\'s encrypted public key: ' + withUserEncryptedPublicKey);

    var privateKey = localStorage.getItem('prvKey');
    var withUserPublicKey = decryptWithRSA(privateKey, withUserEncryptedPublicKey);
    console.log('with user\'s public key: ' + withUserPublicKey);

    var verificationResult = verifyWithRSA(serverPublicKey, withUserEncryptedPublicKey, signature);
    console.log('verificationResult: ' + verificationResult);

    if (verificationResult) {
        handshakeCompletedSuccessfully = true;
    }
});

connection.on('SetAesKey', function (encryptedAesKey, signature) {
    if (!handshakeCompletedSuccessfully) {
        return;
    }

    var aesKeyVerificationResult = verifyWithRSA(serverPublicKey, encryptedAesKey, signature);
    console.log('aesKeyVerificationResult: ' + aesKeyVerificationResult);
    if (!aesKeyVerificationResult) {
        return;
    }

    console.log('Received AES encrypted key: ' + encryptedAesKey);

    var privateKey = localStorage.getItem('prvKey');
    var aesKey = decryptWithRSA(privateKey, encryptedAesKey);
    console.log('Received AES key: ' + aesKey);
});


connection.start().then(function () {
    sendPublicKey();
});

$('#sendButton').click(e => {
    e.preventDefault();

    var messageInput = $('#messageInput');
    var message = messageInput.val();
    messageInput.val('');

    var encryptedMessage = encryptMessage(message);
    connection.invoke('SendMessage', selectedUser, encryptedMessage);
    appendMessage('You', message);
});

function appendUser(userEmail) {
    var userItem = document.createElement('a');
    userItem.textContent = userEmail;
    userItem.href = '#';
    userItem.id = userEmail;
    userItem.className = 'list-group-item list-group-item-action list-group-item-light';
    userItem.setAttribute('data-toggle', 'tab');
    userItem.setAttribute('role', 'tab');
    userItem.onclick = onUserClick;
    document.getElementById('usersList').appendChild(userItem);
}

function appendMessage(user, message) {
    var element = document.createElement('p');
    element.textContent = user + ': ' + message;
    document.getElementById('messagesList').appendChild(element);
}

function onUserClick(e) {
    e.preventDefault();
    $(this).tab('show');
}

function onUserSelected(e) {
    selectedUser = e.target.id;
    document.getElementById('startConversation').disabled = false;
}

function startHandshake() {
    connection.invoke('StartHandshake', selectedUser).then(function (response) {
        console.log('selected user\'s encrypted public key: ' + response.selectedUserEncryptedPublicKey);

        var privateKey = localStorage.getItem('prvKey');
        var selectedUserPublicKey = decryptWithRSA(privateKey, response.selectedUserEncryptedPublicKey);
        console.log('selected user\'s public key: ' + selectedUserPublicKey);

        var verificationResult = verifyWithRSA(serverPublicKey, response.selectedUserEncryptedPublicKey, response.signature);
        console.log('verificationResult: ' + verificationResult);

        if (verificationResult) {
            generateAesKey().then(() => {
                console.log('AES key: ' + aesKey);

                var base64AesKey = byteArrayToBase64(aesKey);

                var aesKeyEncrytedForSelectedUser = encryptWithRSA(selectedUserPublicKey, base64AesKey);
                console.log("encrypted AES key for selected user: " + aesKeyEncrytedForSelectedUser);

                var aesKeyEncrytedForServer = encryptWithRSA(serverPublicKey, aesKeyEncrytedForSelectedUser);
                console.log("encrypted AES key for server: " + aesKeyEncrytedForServer);

                connection.invoke('SubmitAesKey', selectedUser, aesKeyEncrytedForServer)
                    .then(startConversation);
            });
        }
    });
}

function startConversation() {
    $('#startConversation').prop('disabled', true);
    $('#conversation').show();
}

function generateAesKey() {
    return crypto.subtle.generateKey({ name: "AES-CTR", length: 256 }, true, ["encrypt", "decrypt"])
        .then(key => crypto.subtle.exportKey('raw', key))
        .then(rawKey => aesKey = new Uint8Array(rawKey));
}

function encryptMessage(message) {
    var messageBytes = aesjs.utils.utf8.toBytes(message);

    var aesCtr = new aesjs.ModeOfOperation.ctr(aesKey);
    var encryptedMessageBytes = aesCtr.encrypt(messageBytes);

    return aesjs.utils.hex.fromBytes(encryptedMessageBytes);
}

function decryptMessage(encryptedMessage) {
    var encryptedMessageBytes = aesjs.utils.hex.toBytes(encryptedMessage);

    var aesCtr = new aesjs.ModeOfOperation.ctr(aesKey);
    var decryptedMessageBytes = aesCtr.decrypt(encryptedMessageBytes);

    return aesjs.utils.utf8.fromBytes(decryptedMessageBytes);
}

function sendPublicKey() {
    var clientPublicKey = localStorage.getItem('pubKey');
    if (clientPublicKey === null) {
        generateRSAKeyAndStore();
        console.log('RSA key-pair generated and stored');

        clientPublicKey = localStorage.getItem('pubKey');
    }

    console.log('clientPublicKey: ' + clientPublicKey);
    var encryptedClientPublicKey = encryptWithRSA(serverPublicKey, clientPublicKey);
    console.log('encryptedClientPublicKey: ' + encryptedClientPublicKey);

    connection.invoke('SetClientPublicKey', encryptedClientPublicKey);
}

function byteArrayToBase64(byteArray) {
    var binary = '';
    var bytes = new Uint8Array(byteArray);
    var len = bytes.byteLength;
    for (var i = 0; i < len; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}

$('#startConversation').click(startHandshake);

$(document).ready(function () {
    $(document).on('shown.bs.tab', 'a[data-toggle="tab"]', onUserSelected);

    $.get('server_2048_pub.pem', function (data) {
        serverPublicKey = data;
    });
});