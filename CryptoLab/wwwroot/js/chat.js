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

connection.on('StartHandshakeRequested', function (withUser, withUserEncryptedPublicKey, signature) {
    handshakeCompletedSuccessfully = false;
    console.log('with user\'s encrypted public key: ' + withUserEncryptedPublicKey);

    var verificationResult = verifyWithRSA(serverPublicKey, withUserEncryptedPublicKey, signature);
    console.log('verificationResult: ' + verificationResult);

    if (!verificationResult) {
        return;
    }

    var privateKey = localStorage.getItem('prvKey');
    var withUserPublicKey = decryptWithRSA(privateKey, withUserEncryptedPublicKey);
    console.log('with user\'s public key: ' + withUserPublicKey);

    handshakeCompletedSuccessfully = true;
    $('#' + $.escapeSelector(withUser)).tab('show');
});

connection.on('SetAesKey', function (encryptedAesKey, signature) {
    if (!handshakeCompletedSuccessfully) {
        return;
    }

    console.log('Received AES encrypted key: ' + encryptedAesKey);

    var aesKeyVerificationResult = verifyWithRSA(serverPublicKey, encryptedAesKey, signature);
    console.log('aesKeyVerificationResult: ' + aesKeyVerificationResult);
    if (!aesKeyVerificationResult) {
        return;
    }

    var privateKey = localStorage.getItem('prvKey');
    var decryptedAesKey = decryptWithRSA(privateKey, atob(encryptedAesKey));
    aesKey = new Uint8Array(base64ToByteArray(decryptedAesKey));
    console.log('Received AES key: ');
    console.log(aesKey);

    startConversation();
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
    userItem.className = 'list-group-item list-group-item-action';
    userItem.setAttribute('data-toggle', 'tab');
    userItem.setAttribute('role', 'tab');
    userItem.onclick = onUserClick;

    if (selectedUser) {
        userItem.classList.add('disabled');
    }

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
    $('.list-group-item').addClass('disabled');
}

function startHandshake() {
    connection.invoke('StartHandshake', selectedUser).then(function (response) {
        console.log('selected user\'s encrypted public key: ' + response.selectedUserEncryptedPublicKey);

        var verificationResult = verifyWithRSA(serverPublicKey, response.selectedUserEncryptedPublicKey, response.signature);
        console.log('verificationResult: ' + verificationResult);

        if (!verificationResult) {
            return;
        }

        var privateKey = localStorage.getItem('prvKey');
        var selectedUserPublicKey = decryptWithRSA(privateKey, response.selectedUserEncryptedPublicKey);
        console.log('selected user\'s public key: ' + selectedUserPublicKey);

        aesKey = generateAesKey();

        var base64AesKey = byteArrayToBase64(aesKey);

        var aesKeyEncryptedForSelectedUser = encryptWithRSA(selectedUserPublicKey, base64AesKey);
        console.log('encrypted AES key for selected user: ' + aesKeyEncryptedForSelectedUser);

        var aesKeyEncryptedForServer = encryptWithRSA(serverPublicKey, aesKeyEncryptedForSelectedUser);
        console.log('encrypted AES key for server: ' + aesKeyEncryptedForServer);

        connection.invoke('SubmitAesKey', selectedUser, aesKeyEncryptedForServer)
            .then(startConversation);
    });
}

function startConversation() {
    $('#startConversation').prop('disabled', true);
    $('#conversation').show();
}

function generateAesKey() {
    var key = new Uint8Array(32);
    crypto.getRandomValues(key);

    console.log('AES key generated: ' + key);

    return key;
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

function base64ToByteArray(base64) {
    var binaryString = atob(base64);
    var len = binaryString.length;
    var bytes = new Uint8Array(len);
    for (var i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes.buffer;
}

$('#startConversation').click(startHandshake);

$(document).ready(function () {
    $(document).on('shown.bs.tab', 'a[data-toggle="tab"]', onUserSelected);

    $.get('server_2048_pub.pem', function (data) {
        serverPublicKey = data;
    });
});