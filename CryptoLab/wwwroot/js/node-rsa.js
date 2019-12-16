window.generateRSAKeyAndStore = function () {
	var NodeRSA = require('node-rsa');
	var key = new NodeRSA({b: 512});
	localStorage.setItem("prvKey", key.exportKey("pkcs8-private"));
	localStorage.setItem("pubKey", key.exportKey("public"));
	return key;
};

window.encryptWithRSA = function (key, text) {
	var NodeRSA = require('node-rsa');
	var key = new NodeRSA(key);
	key.setOptions({encryptionScheme: 'pkcs1'});
	console.log('MaxMessageSize (bytes): ' + key.getMaxMessageSize());
	return key.encrypt(text, 'base64');	
};

window.decryptWithRSA = function (key, text) {
	var NodeRSA = require('node-rsa');
	var key = new NodeRSA(key);
	key.setOptions({encryptionScheme: 'pkcs1'});
	
	return key.decrypt(text, 'utf8');	
};