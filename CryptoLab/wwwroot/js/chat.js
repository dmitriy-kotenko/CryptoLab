var connection = new signalR.HubConnectionBuilder().withUrl('/chatHub').build();

var selectedUser;

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

var serverPublicKey;

// An example 128-bit key (16 bytes * 8 bits/byte = 128 bits)
var key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

function startConversation() {

}

function encryptMessage(message) {
    var messageBytes = aesjs.utils.utf8.toBytes(message);

    var aesCtr = new aesjs.ModeOfOperation.ctr(key);
    var encryptedMessageBytes = aesCtr.encrypt(messageBytes);

    return aesjs.utils.hex.fromBytes(encryptedMessageBytes);
}

function decryptMessage(encryptedMessage) {
    var encryptedMessageBytes = aesjs.utils.hex.toBytes(encryptedMessage);

    var aesCtr = new aesjs.ModeOfOperation.ctr(key);
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


$('#startConversation').click(e => {
    $('#' + e.target.id).prop('disabled', true);

    startConversation();

    $('#conversation').show();
});

$(document).ready(function () {
    $(document).on('shown.bs.tab', 'a[data-toggle="tab"]', onUserSelected);

    $.get('server_2048_pub.pem', function (data) {
        serverPublicKey = data;
    });
});