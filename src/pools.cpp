#include "main.h"

unordered_map<size_t, vector<void*>*> pools;
StringMemory *strmem;
char *strbuf;
long long strbufsz;

void init_pools( void )
{
	pools.reserve( 1000 );
	
	strmem = (StringMemory*)halloc( sizeof(StringMemory) );
	new(strmem) StringMemory();

	strbufsz = 1024*1024;
	strbuf = malloc( strbufsz );
}

void *halloc( size_t sz )
{
	unordered_map<size_t, vector<void*>*>::iterator it;
	vector <void*> *pool;
	if( (it=pools.find(sz)) != pools.end() ) {
		pool = it->second;
		if( pool->size() > 0 ) {
			void *ptr = pool->back();
			pool->pop_back();
			return ptr;
		}
	}
	return malloc(sz);
}
void hfree( void *ptr, size_t sz )
{
	unordered_map<size_t, vector<void*>*>::iterator it;
	vector <void*> *pool;
	if( (it=pools.find(sz)) != pools.end() ) {
		pool = it->second;
		pool->push_back(ptr);
	} else {
		pool = new vector<void*>();
		pool->push_back(ptr);
		pools[sz] = pool;
	}
}

StringMemory::StringMemory()
{
	size_t size = 1024*1024;
	char *zero = malloc( size );
	items_ptr.insert( StringMemoryItem(zero, size) );
	items_sz.insert( StringMemoryItem2(zero, size) );
}

StringMemory::~StringMemory()
{
	set<StringMemoryItem>::iterator it;
	for( it=items_ptr.begin(); it!=items_ptr.end(); it++ ) {
		free( it->ptr );
	}
}

char *StringMemory::Alloc( size_t sz )
{
	set<StringMemoryItem2>::iterator it;
	char *ptr;

	it = items_sz.lower_bound( StringMemoryItem2(NULL, sz) );
	if( it != items_sz.end() ) {
		StringMemoryItem2 &item = *it;
		ptr = item.ptr;
		if( item.size == sz ) {
			items_sz.erase( it );
			items_ptr.erase( item );
		} else {
			item.ptr += sz;
			item.size -= sz;
		}
		return ptr;
	}
	// no free block found, allocate new
	size_t size = 1024*1024;
	char *zero = malloc( size );
	ptr = zero;
	zero += sz;
	size -= sz;
	items_ptr.insert( StringMemoryItem(zero, size) );
	items_sz.insert( StringMemoryItem2(zero, size) );
	return ptr;
}

void StringMemory::Free( char *ptr, size_t sz )
{
	StringMemoryItem item(ptr, sz);
	set<StringMemoryItem>::iterator it;
	it = items_ptr.lower_bound( item );
	if( it != items_ptr.end() && it != items_ptr.begin() ) {
		StringMemoryItem &item2 = *(it);
		if( ptr + sz == item2.ptr ) {
			items_sz.erase( item2 );
			item2.ptr = ptr;
			item2.size += sz;
			items_sz.insert( item2 );
			return;
		}
		item2 = *(it-1);
		if( item2.ptr + item2.size == ptr ) {
			items_sz.erase( item2 );
			item2.size += sz;
			items_sz.insert( item2 );
			return;
		}
	}
	items_ptr.insert( item );
	items_sz.insert( StringMemoryItem2(ptr, sz) );
}

/*
StringTrie::StringTrie()
{
	str = NULL;
	for( int i=0; i<27; i++ ) {
		t[i] = NULL;
	}
}

StringTrie::~StringTrie()
{
	for( int i=0; i<27; i++ ) {
		if( t[i] ) {
			delete t[i];
		}
	}
}

StringTrie::Add(const char *str, int offset)
{
	int i, c;
	const char *ptr;
	StringTrie *st = this;

	for( ptr = str+offset; ptr; ptr++ ) {
		c = *ptr;
		if( c >= 'a' && c <= 'z' ) {
			c -= 'a';
		} else if( c >= 'A' && c <= 'Z' ) {
			c -= 'A';
		} else {
			c = 26;
		}
		if( !st->t[c] ) {
			st->t[c] = new StringTrie();
			st->t[c]->str = str;
			st = t[c];
		}
	}
}
*/
