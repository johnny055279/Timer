const timeDisplay = document.querySelector('.display__time-left');
const countDownTitleDisplay = document.querySelector('#countdownTitle');
const beepSelector = document.getElementById('beepSelect');
const audio = document.createElement("audio");
var countDownDate = new Date();
let canPlay = true;
let then = Date.now();

let countdown = setInterval(() => {
    const secondsLeft = Math.round((then - Date.now()) / 1000);
    if (secondsLeft <= 0) {
        then = Date.now();
        timeDisplay.textContent = "00:00:00";
        if (canPlay) {
            playAudio();
            canPlay = false;
        }
        return;
    }
    displayTimeLeft(secondsLeft);
}, 1000)

function displayTimeLeft(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = (Math.floor(seconds / 60)) % 60;
    const reminderSeconds = seconds % 60;
    const display = `${hours < 10 ? '0' : ''}${hours}:${minutes < 10 ? '0' : ''}${minutes}:${reminderSeconds < 10 ? '0' : ''}${reminderSeconds}`;
    timeDisplay.textContent = display;
}

function IncreaseTime(number) {
    then += number * 1000 * 60
    canPlay = true;
}

function Reset() {
    then = Date.now();
    timeDisplay.textContent = "00:00:00";
}

function UpdateTitle(text) {
    countDownTitleDisplay.textContent = text;
}

function playAudio() {
    audio.src = `beeps/${beepSelector.value}`;
    audio.play();
}