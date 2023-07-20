import os
import tempfile
import re
import io

directory = os.path.dirname(os.path.realpath(__file__))

os.chdir(directory)

fp = tempfile.NamedTemporaryFile()
fp.close();

os.system('dotnet build Library\E6dWebApp.csproj --configuration "Release" | tee {}'.format(fp.name))

package = 'Net.Leksi.E6dWebApp'
pack = ''
version = ''

f = open(fp.name)
for line in f:
	if '.nupkg' in line:
		pack = re.match(r'^[^"]+".+?([^"\\]+)"', line).group(1)
		break
f.close()

version = pack[len(package) + 1:-len('.nupkg')]

print(version)

os.system('del /q Release\\*')
os.system('copy ..\\nuget.local\\{} Release'.format(pack))

helloer = 'Demo\\Helloer\\Helloer.csproj'
implementer = 'Demo\\InterfaceImplementer\\InterfaceImplementer.csproj'
test = 'Demo\\UnitTesting\\TestApplication\\TestApplication.csproj'
server = 'Demo\\UnitTesting\\Server\\Server.csproj'

for proj in [helloer, implementer, test]:
	os.system('dotnet add {} package {} --source ..\\nuget.local'.format(proj, package))

for proj in [helloer, implementer, test, server]:
	os.system('dotnet build {} --configuration "Release"'.format(proj))

output = tempfile.TemporaryDirectory()

for proj in [helloer, implementer, server]:
	os.system('del /q {}\\*'.format(output.name))
	os.system('dotnet publish {} --configuration "Release" --no-build --output {} | tee {}'.format(proj, output.name, fp.name))
	name = ''
	path = ''
	f = open(fp.name)
	for line in f:
		if '->' in line:
			match = re.match(r'^\s*(.+?)\s*->\s*(.+)', line)
			print(match)
			name = match.group(1)
			path = match.group(2)
			break
	f.close()
	print(name, path)
	zipName = '{}-{}-7z.exe'.format(name, version)
	os.system('"C:\\Program Files\\7-Zip\\7z.exe\" a -sfx Release\\{} {}*'.format(zipName, path))

os.system('git add .')

tag = 'v' + version
hasTag = False

os.system('git tag | tee {}'.format(fp.name))

f = open(fp.name)
for line in f:
	if line.strip() == tag:
		hasTag = True
		break
f.close()

if hasTag:
	print('Tag {} already exists!'.format(tag))
	os.system('git commit --allow-empty-message -a -m ""')
else:
	os.system('git commit --allow-empty-message -a -m "{}"'.format(tag))
	os.system('git tag {}'.format(tag))

for remote in ['github', 'sofo']:
	os.system('git push {} master'.format(remote))
	if not hasTag:
		os.system('git push {} {}'.format(remote, tag))

if True: #not hasTag:
	os.chdir('wiki')
	os.system('git pull github master')
	files = [f for f in os.listdir('.') if re.match(r'[^-]+-(?:Helloer|InterfaceImplementer|UnitTesting)-(?:ru|en)\.md', f)]
	for file in files:
		f = io.open(file, mode='r', encoding='utf-8')
		for line in f:
			match = re.match(r'\(\.\./releases/download/v(\d+\.\d+\.\d+)/[^-]+-(\d+\.\d+\.\d+)-7z\.exe\)', line)
			if match != None:
				print(line)
